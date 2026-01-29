using RabbitMQ.Client;
using System.Collections.Concurrent;

namespace Travel.WebApi.Infrastructure.Rabbit;

public interface IRabbitChannelPool
{
    ValueTask<IChannel> AcquireAsync(CancellationToken cancellationToken = default);
    ValueTask ReleaseAsync(IChannel channel);
}

public class RabbitChannelPool(
    IRabbitConnection rabbitConnection,
    ILogger<RabbitChannelPool> logger) : IRabbitChannelPool, IAsyncDisposable
{
    private const int DefaultMaxSize = 10;

    private readonly IConnection _connection = rabbitConnection.Connection ?? throw new ArgumentNullException(nameof(rabbitConnection));
    private readonly ILogger<RabbitChannelPool> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentBag<IChannel> _channels = [];
    private readonly SemaphoreSlim _semaphore = new(DefaultMaxSize);
    private int _currentSize = 0;
    private readonly int _maxSize = DefaultMaxSize; //TODO: to config
    private bool _disposed;

    public async ValueTask<IChannel> AcquireAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_connection.IsOpen)
        {
            throw new InvalidOperationException("RabbitMQ connection is closed");
        }

        await _semaphore.WaitAsync(cancellationToken);

        // Try to get an existing channel
        while (_channels.TryTake(out var channel))
        {
            if (channel.IsOpen)
            {
                _logger.LogTrace("Reused existing channel from pool (Pool size: {Size}/{MaxSize})", _currentSize, _maxSize);
                return channel;
            }

            // Channel is closed, dispose it and decrement counter
            try
            {
                await channel.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing closed channel from pool");
            }

            Interlocked.Decrement(ref _currentSize);
            _logger.LogTrace("Removed closed channel from pool (Pool size: {Size}/{MaxSize})", _currentSize, _maxSize);
        }

        // No valid channel found, create a new one
        try
        {
            var newChannel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            Interlocked.Increment(ref _currentSize);
            _logger.LogTrace("Created new channel (Pool size: {Size}/{MaxSize})", _currentSize, _maxSize);
            return newChannel;
        }
        catch (Exception ex)
        {
            _semaphore.Release();
            _logger.LogError(ex, "Failed to create new channel for pool");
            throw;
        }
    }

    public ValueTask ReleaseAsync(IChannel? channel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (channel == null)
        {
            _semaphore.Release();
            return ValueTask.CompletedTask;
        }

        if (channel.IsOpen && _connection.IsOpen)
        {
            _channels.Add(channel);
            _logger.LogTrace("Returned channel to pool (Pool size: {Size}/{MaxSize})", _currentSize, _maxSize);
        }
        else
        {
            try
            {
                channel.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing channel during release");
            }

            Interlocked.Decrement(ref _currentSize);
            _logger.LogTrace("Disposed closed channel during release (Pool size: {Size}/{MaxSize})", _currentSize, _maxSize);
        }

        _semaphore.Release();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        _logger.LogInformation("Disposing RabbitMQ channel pool with {Count} channels", _currentSize);

        while (_channels.TryTake(out var channel))
        {
            try
            {
                if (channel.IsOpen)
                {
                    await channel.CloseAsync();
                }
                await channel.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing channel during pool disposal");
            }
        }

        _semaphore.Dispose();
        _logger.LogInformation("RabbitMQ channel pool disposed");

        GC.SuppressFinalize(this);
    }
}
