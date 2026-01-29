using System.Net.Mime;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Text;
using System.Text.Json;
using Travel.WebApi.Domain.Events;
using Travel.WebApi.Infrastructure.Rabbit;
using Travel.WebApi.Infrastructure.Rabbit.Configuration;

namespace Travel.WebApi.Application;

public interface IOrderMessagePublisher
{
    ValueTask PublishAsync(OrderCreatedEvent message, CancellationToken cancellationToken);
}

public class OrderMessagePublisher(
    IRabbitChannelPool channelPool,
    IOptions<RabbitConfig> rabbitOptions,
    ILogger<OrderMessagePublisher> logger) : IOrderMessagePublisher
{
    private readonly IRabbitChannelPool _channelPool = channelPool;
    private readonly RabbitConfig _rabbitSettings = rabbitOptions.Value;
    private readonly ILogger<OrderMessagePublisher> _logger = logger;

    public async ValueTask PublishAsync(OrderCreatedEvent message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(message.CorrelationId))
        {
            message.CorrelationId = Guid.NewGuid().ToString();
        }

        IChannel? channel = null;
        try
        {
            channel = await _channelPool.AcquireAsync(cancellationToken);
            
            // Verify channel is open before using it
            if (!channel.IsOpen)
            {
                throw new InvalidOperationException("Channel is closed");
            }
            
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
                ContentType = MediaTypeNames.Application.Json,
                CorrelationId = message.CorrelationId,
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _rabbitSettings.QueueName,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogDebug("Published order {OrderId} to RabbitMQ queue {QueueName} (CorrelationId: {CorrelationId}, MessageId: {MessageId})", 
                message.OrderId, _rabbitSettings.QueueName, message.CorrelationId, properties.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Unexpected error while publishing order {OrderId} to RabbitMQ queue {QueueName}", 
                message.OrderId, _rabbitSettings.QueueName);
            throw new InvalidOperationException("Failed to publish message to broker", ex);
        }
        finally
        {
            if (channel != null)
            {
                await _channelPool.ReleaseAsync(channel);
            }
        }
    }
}
