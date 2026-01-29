using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Travel.WebApi.Data;
using Travel.WebApi.Domain.Entities;
using Travel.WebApi.Domain.Events;

namespace Travel.WebApi.Application;

public class OutboxProcessorService(
    IServiceProvider serviceProvider,
    ILogger<OutboxProcessorService> logger) : BackgroundService
{
    private const int ProcessingIntervalSeconds = 5;
    private const int BatchSize = 10;
    private const int MaxRetryCount = 5;
    
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<OutboxProcessorService> _logger = logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(ProcessingIntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Outbox processor service stopping due to cancellation");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            try
            {
                await Task.Delay(_processingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Outbox processor service stopped");
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IOrderMessagePublisher>();

        var pendingMessages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < MaxRetryCount)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Processing {Count} outbox messages", pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            try
            {
                await ProcessMessageAsync(message, publisher, cancellationToken);
                
                message.ProcessedAt = DateTime.UtcNow;
                message.Error = null;
                message.RetryCount = 0;

                _logger.LogInformation("Successfully processed outbox message {MessageId} of type {Type}", 
                    message.Id, message.Type);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Message broker is currently unavailable"))
            {
                // RabbitMQ broker is down - transient error, increment retry
                message.RetryCount++;
                message.Error = $"Broker unavailable: {ex.InnerException?.Message ?? ex.Message}";

                if (message.RetryCount >= MaxRetryCount)
                {
                    _logger.LogError(ex, 
                        "Outbox message {MessageId} reached max retry count ({MaxRetryCount}) due to broker unavailability. Moving to dead letter.", 
                        message.Id, MaxRetryCount);
                }
                else
                {
                    _logger.LogWarning(ex, 
                        "Failed to process outbox message {MessageId} due to broker unavailability (retry {RetryCount}/{MaxRetryCount})", 
                        message.Id, message.RetryCount, MaxRetryCount);
                }
            }
            catch (JsonException ex)
            {
                // Permanent error - bad data, don't retry
                message.RetryCount = MaxRetryCount;
                message.Error = $"Invalid JSON payload: {ex.Message}";
                
                _logger.LogError(ex, 
                    "Outbox message {MessageId} has invalid JSON payload. Moving to dead letter without retry.", 
                    message.Id);
            }
            catch (Exception ex)
            {
                // Unknown error - increment retry
                message.RetryCount++;
                message.Error = $"{ex.GetType().Name}: {ex.Message}";

                if (message.RetryCount >= MaxRetryCount)
                {
                    _logger.LogError(ex, 
                        "Outbox message {MessageId} reached max retry count ({MaxRetryCount}). Moving to dead letter.", 
                        message.Id, MaxRetryCount);
                }
                else
                {
                    _logger.LogWarning(ex, 
                        "Failed to process outbox message {MessageId} (retry {RetryCount}/{MaxRetryCount})", 
                        message.Id, message.RetryCount, MaxRetryCount);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task ProcessMessageAsync(
        OutboxMessage message, 
        IOrderMessagePublisher publisher, 
        CancellationToken cancellationToken)
    {
        switch (message.Type)
        {
            case nameof(OrderCreatedEvent):
                var orderCreatedEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(message.Payload) 
                    ?? throw new JsonException($"Failed to deserialize {nameof(OrderCreatedEvent)}");
                
                await publisher.PublishAsync(orderCreatedEvent, cancellationToken);
                break;

            default:
                throw new InvalidOperationException($"Unknown outbox message type: {message.Type}");
        }
    }
}
