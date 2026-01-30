using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Travel.WebApi.Pubsub.Messages;
using Travel.WebApi.Pubsub.Rabbit;

namespace Travel.WebApi.Services;

public class OrderMessageConsumer(
    IConnection rabbitConnection, 
    IOptions<RabbitSettings> rabbitOptions, 
    IServiceProvider services, 
    ILogger<OrderMessageConsumer> logger) : BackgroundService
{
    private readonly IConnection _rabbitConnection = rabbitConnection;
    private readonly RabbitSettings _rabbitSettings = rabbitOptions.Value;
    private readonly IServiceProvider _services = services;
    private readonly ILogger<OrderMessageConsumer> _logger = logger;

    private IChannel _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Order background service started.");

        //TODO: move all consuming logic to OrderProcessingQueue.cs and use channel pool
        _channel = await _rabbitConnection.CreateChannelAsync(cancellationToken: stoppingToken);

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
            prefetchSize: 0,
            prefetchCount: 1,
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
    }

    private async Task HandleMessageAsync(object sender, BasicDeliverEventArgs args, CancellationToken cancellationToken)
    {
        try
        {
            var body = args.Body.ToArray();
            var messageJson = Encoding.UTF8.GetString(body);
            var message = JsonSerializer.Deserialize<StartOrderSubmitEvent>(messageJson);

            if (message == null)
            {
                _logger.LogWarning("Received null message from RabbitMQ");
                await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: cancellationToken);
                return;
            }

            using var scope = _services.CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            await orderService.SubmitOrderAsync(message, cancellationToken);

            _logger.LogDebug("Dequeued order {OrderId} from RabbitMQ", message.OrderId);

            await _channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message from RabbitMQ");
            await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true, cancellationToken: cancellationToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping OrderBackgroundService");

        if (_channel?.IsOpen == true)
            await _channel.CloseAsync(cancellationToken);

        _channel?.Dispose();

        await base.StopAsync(cancellationToken);
    }
}
