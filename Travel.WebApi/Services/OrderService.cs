using Microsoft.EntityFrameworkCore;
using Travel.WebApi.Data;
using Travel.WebApi.Domain;
using Travel.WebApi.Pubsub.Messages;
using Travel.WebApi.WebApi.Models;
using Travel.WebApi.WebApi;

namespace Travel.WebApi.Services;

public interface IOrderService
{
    Task<int> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken);
    Task SubmitOrderAsync(StartOrderSubmitEvent submitEvent, CancellationToken cancellationToken);
}

public class OrderService(
    IOrderMessagePublisher queue, 
    AppDbContext db, 
    IMapper mapper,
    ILogger<OrderService> logger) : IOrderService
{
    private readonly IOrderMessagePublisher _queue = queue;
    private readonly AppDbContext _db = db;
    private readonly IMapper _mapper = mapper;
    private readonly ILogger<OrderService> _logger = logger;

    public async Task<int> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var entity = _mapper.ToDomainModel(request);

        _db.Orders.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        //TODO: Add outbox event here for OrderCreatedEvent
        var message = new StartOrderSubmitEvent
        {
            OrderId = entity.Id,
            CorrelationId = Guid.NewGuid().ToString()
        };

        await _queue.PublishAsync(message, cancellationToken);

        return entity.Id;
    }

    public async Task SubmitOrderAsync(StartOrderSubmitEvent submitEvent, CancellationToken cancellationToken)
    {
        //TODO: Transaction is very long, consider using Saga pattern
        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == submitEvent.OrderId, cancellationToken);

            var inventories = await _db.InventoryItems
                .Where(i => order.Items.Select(x => x.ItemId).Contains(i.ItemId))
                .ToListAsync(cancellationToken: cancellationToken);

            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found", submitEvent.OrderId);
                await _db.Database.RollbackTransactionAsync(cancellationToken);
                return;
            }

            if (order.Status == OrderStatuses.Submitted)
            {
                _logger.LogWarning("Order {OrderId} is already submitted", order.Id);
                await _db.Database.RollbackTransactionAsync(cancellationToken);
                return;
            }

            foreach (var orderItem in order.Items)
            {
                var inv = inventories.Single(i => i.ItemId == orderItem.ItemId);
                inv.Reserved += orderItem.Quantity;
                inv.InStock -= orderItem.Quantity;
                if (inv.InStock < 0)
                {
                    _logger.LogWarning("Not enough invertory {ItemId} for order {OrderId}", inv.ItemId, order.Id);
                    await _db.Database.RollbackTransactionAsync(cancellationToken);
                    return;
                }
            }

            // Simulate business logic: validate and apply a simple discount if more than 3 items
            await SimulateBusinessLogicAsync(order, cancellationToken);

            order.Status = OrderStatuses.Submitted;
            await _db.SaveChangesAsync(cancellationToken);
            await _db.Database.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation("Marked order {OrderId} as submitted. Processed orders - {ProcessedOrdersNumber}", order.Id, Metrics.IncrementProcessedOrdersNumber());
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error submitting order {OrderId}", submitEvent.OrderId);
            await _db.Database.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private static async Task SimulateBusinessLogicAsync(Order order, CancellationToken cancellationToken)
    {
        // Example: validation
        //if (order.OrderItems == null || !order.OrderItems.Any())
        //{
        //    throw new InvalidOperationException($"Order {order.Id} has no items");
        //}

        // Example: enrichment - add a small processing delay
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

        // Example: discount calculation - use total quantity across order items
        var totalQuantity = order.Items?.Sum(oi => oi.Quantity) ?? 0;
        if (totalQuantity > 3)
        {
            var discount = order.TotalAmount * 0.05m;
            order.TotalAmount -= discount;

            // If you had audit entries or events, they'd be created here
        }
    }
}
