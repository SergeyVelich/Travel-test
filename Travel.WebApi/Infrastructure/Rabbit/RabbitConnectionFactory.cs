using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Travel.WebApi.Infrastructure.Rabbit.Configuration;

namespace Travel.WebApi.Infrastructure.Rabbit;

public interface IRabbitMQConnectionFactory
{
    Task<IConnection> GetConnectionAsync(string clientProvidedName, CancellationToken cancellationToken);
}

public class RabbitConnectionFactory(
    IOptions<RabbitConfig> rabbitOptions,
    ILogger<RabbitConnectionFactory> logger) : IRabbitMQConnectionFactory
{
    private const int NetworkRecoveryIntervalSeconds = 10;
    private const int HeartbeatIntervalSeconds = 60;
    
    private readonly RabbitConfig _rabbitConfig = rabbitOptions.Value;

    public async Task<IConnection> GetConnectionAsync(string clientProvidedName, CancellationToken cancellationToken)
    {
        return await CreateConnectionWithRetryAsync(clientProvidedName, cancellationToken);
    }

    private async Task<IConnection> CreateConnectionWithRetryAsync(
        string? clientProvidedName,
        CancellationToken cancellationToken)
    {
        //TODO: Use Polly for more robust retry logic with exponential backoff and jitter
        const int maxRetries = 10;
        const int delayMilliseconds = 2000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await CreateConnectionAsync(clientProvidedName, cancellationToken);
            }
            catch (Exception ex) when (i < maxRetries - 1)
            {
                logger.LogWarning(
                    "Failed to connect to RabbitMQ (attempt {Attempt}/{MaxRetries}): {Message}",
                    i + 1, maxRetries, ex.Message);
                await Task.Delay(delayMilliseconds, cancellationToken);
            }
        }

        throw new InvalidOperationException("Failed to connect to RabbitMQ after all retries");
    }

    private async Task<IConnection> CreateConnectionAsync(string? clientProvidedName, CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_rabbitConfig.ConnectionString),
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(NetworkRecoveryIntervalSeconds),
            RequestedHeartbeat = TimeSpan.FromSeconds(HeartbeatIntervalSeconds),
            ClientProvidedName = clientProvidedName ?? string.Empty
        };

        var connection = await factory.CreateConnectionAsync(cancellationToken);
            
        logger.LogInformation(
            "RabbitMQ connection established. Endpoint: {Endpoint}, ClientName: {ClientName}",
            factory.Endpoint,
            factory.ClientProvidedName);

        connection.ConnectionShutdownAsync += async (sender, args) =>
        {
            logger.LogWarning(
                "RabbitMQ connection shutdown. ClientName: {ClientName}, Reason: {ReplyText}, Initiator: {Initiator}",
                clientProvidedName ?? string.Empty,
                args.ReplyText,
                args.Initiator);
                
            await Task.CompletedTask;
        };

        connection.ConnectionRecoveryErrorAsync += async (sender, args) =>
        {
            logger.LogError(
                args.Exception,
                "RabbitMQ connection recovery error. ClientName: {ClientName}",
                clientProvidedName ?? string.Empty);
                
            await Task.CompletedTask;
        };

        return connection;
    }
}
