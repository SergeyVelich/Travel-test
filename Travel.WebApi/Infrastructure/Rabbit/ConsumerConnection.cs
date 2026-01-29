using RabbitMQ.Client;

namespace Travel.WebApi.Infrastructure.Rabbit;

public interface IRabbitConnection : IAsyncDisposable
{
    IConnection Connection { get; }
    Task InitializeAsync(string clientProvidedName, CancellationToken cancellationToken);
}

public interface IProducerConnection : IRabbitConnection { }

public interface IConsumerConnection : IRabbitConnection { }

public class ConsumerConnection(IRabbitMQConnectionFactory factory) : IProducerConnection, IConsumerConnection
{
    private IConnection? _connection;
    private bool _disposed;

    public IConnection Connection => _connection ?? throw new InvalidOperationException("Connection not initialized. Call InitializeAsync first.");

    public async Task InitializeAsync(string clientProvidedName, CancellationToken cancellationToken)
    {
        if (_connection != null)
            return;

        _connection = await factory.GetConnectionAsync(clientProvidedName, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_connection?.IsOpen == true)
        {
            await _connection.CloseAsync();
        }
        
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
