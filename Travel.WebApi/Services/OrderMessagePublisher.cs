using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using Travel.WebApi.Pubsub.Messages;
using Travel.WebApi.Pubsub.Rabbit;

namespace Travel.WebApi.Services;

public interface IOrderMessagePublisher
{
    ValueTask PublishAsync(StartOrderSubmitEvent message, CancellationToken cancellationToken);
}

public class OrderMessagePublisher(
    IConnection connection,
    IOptions<RabbitSettings> rabbitOptions,
    ILogger<OrderMessagePublisher> logger) : IOrderMessagePublisher
{
    private readonly IConnection _connection = connection;
    private readonly RabbitSettings _rabbitSettings = rabbitOptions.Value;
    private readonly ILogger<OrderMessagePublisher> _logger = logger;

    public async ValueTask PublishAsync(StartOrderSubmitEvent message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        //TODO: Use channel pool here
        using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        
        // Declare queue
        await channel.QueueDeclareAsync(
            queue: _rabbitSettings.QueueName,
            durable: _rabbitSettings.Durable,
            exclusive: false,
            autoDelete: _rabbitSettings.AutoDelete,
            arguments: null,
            cancellationToken: cancellationToken);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var properties = new BasicProperties
        {
            Persistent = _rabbitSettings.Durable,
            ContentType = "application/json"
        };

        if (!string.IsNullOrWhiteSpace(message.CorrelationId))
        {
            properties.CorrelationId = message.CorrelationId;
        }

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _rabbitSettings.QueueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogDebug("Enqueued order {OrderId} to RabbitMQ", message.OrderId);
    }
}
