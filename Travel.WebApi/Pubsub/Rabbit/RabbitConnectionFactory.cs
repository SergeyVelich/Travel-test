using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Travel.WebApi.Pubsub.Rabbit;

public interface IRabbitMQConnectionFactory
{
    IConnection GetConnection();
}

public class RabbitConnectionFactory : IRabbitMQConnectionFactory, IDisposable
{
    private readonly RabbitSettings _rabbitSettings;
    private readonly ILogger<RabbitConnectionFactory> _logger;

    private readonly Lazy<IConnection> _connection;
    private bool _disposed;

    public RabbitConnectionFactory(
        IOptions<RabbitSettings> rabbitOptions,
        ILogger<RabbitConnectionFactory> logger)
    {
        _rabbitSettings = rabbitOptions.Value;
        _logger = logger;

        _connection = new Lazy<IConnection>(
            CreateConnection,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IConnection GetConnection()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        //TODO: this delay for docker-compose start, remove it later
        Task.Delay(20_000).GetAwaiter().GetResult();
        return _connection.Value;
    }

    private IConnection CreateConnection()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(_rabbitSettings.ConnectionString),
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                ClientProvidedName = "Travel.WebApi"
            };

            var connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            
            _logger.LogInformation(
                "RabbitMQ connection established. Endpoint: {Endpoint}",
                factory.Endpoint);

            connection.ConnectionShutdownAsync += async (sender, args) =>
            {
                _logger.LogWarning(
                    "RabbitMQ connection shutdown. Reason: {ReplyText}, Initiator: {Initiator}",
                    args.ReplyText,
                    args.Initiator);
                
                await Task.CompletedTask;
            };

            connection.ConnectionRecoveryErrorAsync += async (sender, args) =>
            {
                _logger.LogError(
                    args.Exception,
                    "RabbitMQ connection recovery error");
                
                await Task.CompletedTask;
            };

            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create RabbitMQ connection");
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_connection.IsValueCreated)
        {
            try
            {
                var conn = _connection.Value;
                if (conn.IsOpen)
                {
                    conn.CloseAsync().GetAwaiter().GetResult();
                }
                conn.Dispose();
                
                _logger.LogInformation("RabbitMQ connection disposed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while disposing RabbitMQ connection");
            }
        }

        GC.SuppressFinalize(this);
    }
}
