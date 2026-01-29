using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Travel.WebApi.Domain.Events;
using Travel.WebApi.Infrastructure.Rabbit;
using Travel.WebApi.Infrastructure.Rabbit.Configuration;

namespace Travel.WebApi.Application;

public class OrderMessageConsumer(
    IRabbitConnection consumerConnection,
    IOptions<RabbitConfig> rabbitOptions, 
    IServiceProvider services, 
    ILogger<OrderMessageConsumer> logger) : BackgroundService
{  
    private readonly IRabbitConnection _consumerConnection = consumerConnection;
    private readonly RabbitConfig _rabbitSettings = rabbitOptions.Value;
    private readonly IServiceProvider _services = services;
    private readonly ILogger<OrderMessageConsumer> _logger = logger;
    private readonly SemaphoreSlim _channelLock = new(1, 1);

    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Order background service started.");

        // Initialize connection
        await _consumerConnection.InitializeAsync("Consumer", stoppingToken);
        var rabbitConnection = _consumerConnection.Connection;

        _channel = await rabbitConnection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Declare queue
        await _channel.QueueDeclareAsync(
            queue: _rabbitSettings.QueueName,
            durable: _rabbitSettings.Durable,
            exclusive: false,
            autoDelete: _rabbitSettings.AutoDelete,
            arguments: null,
            cancellationToken: stoppingToken);

        // Set QoS
        await _channel.BasicQosAsync(
            prefetchSize: _rabbitSettings.PrefetchSize,
            prefetchCount: _rabbitSettings.PrefetchCount,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (sender, ea) =>
            HandleMessageAsync(sender, ea, stoppingToken);

        _logger.LogInformation("RabbitMQ consumer initialized for queue: {QueueName}", _rabbitSettings.QueueName);

        await _channel.BasicConsumeAsync(
            queue: _rabbitSettings.QueueName, 
            autoAck: false, 
            consumer: consumer, 
            cancellationToken: stoppingToken);

        // Keep the method running until cancellation is requested
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessageAsync(object sender, BasicDeliverEventArgs args, CancellationToken cancellationToken)
    {
        var body = args.Body.ToArray();
        var messageJson = Encoding.UTF8.GetString(body);
        var message = JsonSerializer.Deserialize<OrderCreatedEvent>(messageJson);

        if (message == null)
        {
            _logger.LogWarning("Received null message from RabbitMQ");
            await AcknowledgeMessageAsync(args.DeliveryTag, false, false, cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(message.CorrelationId) || !Guid.TryParse(message.CorrelationId, out _))
        {
            _logger.LogError("Invalid or missing CorrelationId in message for order {OrderId}", message.OrderId);
            await AcknowledgeMessageAsync(args.DeliveryTag, false, false, cancellationToken);
            return;
        }

        try
        {
            using var scope = _services.CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            await orderService.SubmitOrderAsync(message, cancellationToken);

            _logger.LogDebug("Dequeued order {OrderId} from RabbitMQ (CorrelationId: {CorrelationId})", 
                message.OrderId, message.CorrelationId);

            await AcknowledgeMessageAsync(args.DeliveryTag, true, false, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message from RabbitMQ");
            await AcknowledgeMessageAsync(args.DeliveryTag, false, true, cancellationToken);
        }
    }

    private async Task AcknowledgeMessageAsync(ulong deliveryTag, bool ack, bool requeue, CancellationToken cancellationToken)
    {
        if (_channel == null)
        {
            _logger.LogError("Channel is null, cannot acknowledge message with deliveryTag {DeliveryTag}", deliveryTag);
            return;
        }

        await _channelLock.WaitAsync(cancellationToken);
        try
        {
            if (ack)
            {
                await _channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken);
            }
            else
            {
                await _channel.BasicNackAsync(deliveryTag, multiple: false, requeue, cancellationToken);
            }
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping OrderBackgroundService");

        await _channelLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel?.IsOpen == true)
                await _channel.CloseAsync(cancellationToken);

            _channel?.Dispose();
        }
        finally
        {
            _channelLock.Release();
        }

        _channelLock.Dispose();

        await base.StopAsync(cancellationToken);
    }
}
