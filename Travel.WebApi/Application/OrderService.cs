using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Travel.WebApi.Data;
using Travel.WebApi.Domain.Entities;
using Travel.WebApi.Domain.Events;

namespace Travel.WebApi.Application;

public interface IOrderService
{
    Task<long> CreateOrderAsync(Order order, CancellationToken cancellationToken);
    Task SubmitOrderAsync(OrderCreatedEvent orderCreatedEvent, CancellationToken cancellationToken);
}

public class OrderService(
    AppDbContext db,
    IApplicationMetrics metrics,
    ILogger<OrderService> logger) : IOrderService
{
    private const string InventoryLockQuery = @"
                    SELECT * FROM ""Inventory"" 
                    WHERE ""ItemId"" = ANY({0})
                    ORDER BY ""ItemId""
                    FOR UPDATE";
    
    private const string ProcessedMessageLockQuery = @"
                    SELECT 1 FROM ""ProcessedMessages"" 
                    WHERE ""EntityId"" = {0} AND ""MessageId"" = {1}
                    FOR UPDATE";
    
    private const int ProcessingDelaySeconds = 1;
    private const int DiscountQuantityThreshold = 3;
    private const decimal DiscountPercentage = 0.05m;
    private const int MinimumInventory = 0;
    
    private readonly AppDbContext _db = db;
    private readonly IApplicationMetrics _metrics = metrics;
    private readonly ILogger<OrderService> _logger = logger;

    public async Task<long> CreateOrderAsync(Order order, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var correlationId = Guid.NewGuid().ToString();

            _db.Orders.Add(order);
            await _db.SaveChangesAsync(cancellationToken);

            var orderCreatedEvent = new OrderCreatedEvent
            {
                OrderId = order.Id,
                CorrelationId = correlationId
            };

            var orderCreatedOutbox = new OutboxMessage
            {
                Type = nameof(OrderCreatedEvent),
                Payload = JsonSerializer.Serialize(orderCreatedEvent),
                CreatedAt = DateTime.UtcNow
            };

            _db.OutboxMessages.Add(orderCreatedOutbox);
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return order.Id;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SubmitOrderAsync(OrderCreatedEvent orderCreatedEvent, CancellationToken cancellationToken)
    {
        // Step 1: Validate CorrelationId
        var messageId = orderCreatedEvent.CorrelationId;
        if (string.IsNullOrWhiteSpace(messageId) || !Guid.TryParse(messageId, out _))
        {
            _logger.LogError("Invalid or missing CorrelationId for order {OrderId}", orderCreatedEvent.OrderId);
            throw new ArgumentException("CorrelationId must be a valid GUID", nameof(orderCreatedEvent));
        }

        var entityId = orderCreatedEvent.OrderId;

        // Step 2: Load order with items (no transaction needed)
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderCreatedEvent.OrderId, cancellationToken);

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found", orderCreatedEvent.OrderId);
            return;
        }

        if (order.Status == OrderStatuses.Submitted)
        {
            _logger.LogInformation("Order {OrderId} already submitted, skipping", order.Id);
            return;
        }

        // Step 3: Apply business logic OUTSIDE transaction (no DB locks held)
        await SimulateBusinessLogicAsync(order, cancellationToken);

        // Step 4: Prepare item IDs for inventory locking
        var itemIds = order.Items.Select(x => x.ItemId).OrderBy(id => id).ToArray();

        // Step 5: BEGIN TRANSACTION - with idempotency check INSIDE transaction
        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // ? IDEMPOTENCY CHECK WITH DATABASE LOCK
            // This ensures only ONE thread can pass this point for the same message
            var alreadyProcessed = await _db.Database
                .SqlQueryRaw<int>(ProcessedMessageLockQuery, entityId, messageId)
                .AnyAsync(cancellationToken);

            if (alreadyProcessed)
            {
                _logger.LogInformation(
                    "Order {OrderId} already processed (detected in transaction with lock), skipping", 
                    orderCreatedEvent.OrderId);
                
                await tx.RollbackAsync(cancellationToken);
                return;
            }

            // Lock inventory rows (FOR UPDATE)
            var inventories = await _db.InventoryItems
                .FromSqlRaw(InventoryLockQuery, itemIds)
                .ToListAsync(cancellationToken);

            // Validate we have all required inventory items
            var missingItems = itemIds.Except(inventories.Select(i => i.ItemId)).ToList();
            if (missingItems.Count > 0)
            {
                _logger.LogError("Missing inventory items {MissingItems} for order {OrderId}", 
                    string.Join(", ", missingItems), order.Id);
                await tx.RollbackAsync(cancellationToken);
                return;
            }

            // Check and update inventory
            var insufficientItems = new List<(long ItemId, int Available, int Required)>();
            
            foreach (var orderItem in order.Items)
            {
                var inv = inventories.Single(i => i.ItemId == orderItem.ItemId);
                
                // Check if enough inventory before making changes
                if (inv.InStock - orderItem.Quantity < MinimumInventory)
                {
                    insufficientItems.Add((inv.ItemId, inv.InStock, orderItem.Quantity));
                    continue;
                }

                inv.Reserved += orderItem.Quantity;
                inv.InStock -= orderItem.Quantity;
            }

            // If any items are insufficient, rollback
            if (insufficientItems.Count > 0)
            {
                _logger.LogWarning(
                    "Insufficient inventory for order {OrderId}. Items: {InsufficientItems}", 
                    order.Id, 
                    string.Join(", ", insufficientItems.Select(x => $"Item {x.ItemId}: Available={x.Available}, Required={x.Required}")));
                
                await tx.RollbackAsync(cancellationToken);
                return;
            }

            // Update order status
            order.Status = OrderStatuses.Submitted;

            // Add processed message for idempotency
            var processedMessage = new ProcessedMessage
            {
                EntityId = entityId,
                MessageId = messageId,
                ProcessedAt = DateTime.UtcNow,
                MessageType = nameof(OrderCreatedEvent)
            };
            _db.ProcessedMessages.Add(processedMessage);

            // Commit all changes atomically
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            // Update metrics after successful commit
            _metrics.IncrementProcessedOrders();
            
            _logger.LogInformation(
                "Successfully submitted order {OrderId}. Total processed orders: {ProcessedOrders}", 
                order.Id, 
                _metrics.GetProcessedOrdersNumber());
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, 
                "Concurrency conflict while submitting order {OrderId}. Another process may have modified the data.", 
                orderCreatedEvent.OrderId);
            
            await tx.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("Concurrency conflict detected. Please retry.", ex);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message?.Contains(ApplicationConstants.Messages.EntityId) == true && 
            ex.InnerException?.Message?.Contains(ApplicationConstants.Messages.CorrelationId) == true)
        {
            // Duplicate message caught by unique constraint - this is actually success (idempotent)
            _logger.LogInformation(
                "Duplicate message {MessageId} for order {OrderId} detected by unique constraint (idempotent)", 
                orderCreatedEvent.CorrelationId, 
                orderCreatedEvent.OrderId);
            
            await tx.RollbackAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting order {OrderId}", orderCreatedEvent.OrderId);
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task SimulateBusinessLogicAsync(Order order, CancellationToken cancellationToken)
    {
        // Simulate external API call or complex calculation
        await Task.Delay(TimeSpan.FromSeconds(ProcessingDelaySeconds), cancellationToken);

        // Calculate discounts
        var totalQuantity = order.Items?.Sum(oi => oi.Quantity) ?? 0;
        if (totalQuantity > DiscountQuantityThreshold)
        {
            var discount = order.TotalAmount * DiscountPercentage;
            order.TotalAmount -= discount;
        }
    }
}