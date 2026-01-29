using Microsoft.EntityFrameworkCore;
using Travel.WebApi.Data;
using Travel.WebApi.Domain;
using Travel.WebApi.Models;

namespace Travel.WebApi.Services;

public interface IOrderService
{
    Task<int> StartSubmitOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken);
    Task SubmitOrderAsync(int orderId, CancellationToken cancellationToken);
}

public class OrderService(
    IOrderProcessingQueue queue, 
    AppDbContext db, 
    IMapper mapper,
    ILogger<OrderService> logger) : IOrderService
{
    private readonly IOrderProcessingQueue _queue = queue;
    private readonly AppDbContext _db = db;
    private readonly IMapper _mapper = mapper;
    private readonly ILogger<OrderService> _logger = logger;

public async Task<int> StartSubmitOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var entity = _mapper.ToDomainModel(request);

        _db.Orders.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        //TODO: Add outbox event here for OrderCreatedEvent
        await _queue.EnqueueAsync(entity.Id, cancellationToken);

        return entity.Id;
    }

    public async Task SubmitOrderAsync(int orderId, CancellationToken cancellationToken)
    {
        var entity = await _db.Orders
                    .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (entity == null)
        {
            _logger.LogWarning("Order {OrderId} not found", orderId);
            return;
        }

        if (entity.Status == OrderStatuses.Submitted)
        {
            _logger.LogWarning("Order {OrderId} is already submitted", orderId);
            await _queue.DequeueAsync(cancellationToken);
            return;
        }

        // Simulate business logic: validate and apply a simple discount if more than 3 items
        await SimulateBusinessLogicAsync(entity, cancellationToken);

        entity.Status = OrderStatuses.Submitted;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Marked order {OrderId} as submitted", entity.Id);
    }

    private static async Task SimulateBusinessLogicAsync(Order order, CancellationToken cancellationToken)
    {
        // Example: validation
        //if (order.Items == null || !order.Items.Any())
        //{
        //    throw new InvalidOperationException($"Order {order.Id} has no items");
        //}

        // Example: enrichment - add a small processing delay
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

        // Example: discount calculation
        if (order.Items.Count > 3)
        {
            var discount = order.TotalAmount * 0.05m;
            order.TotalAmount -= discount;

            // If you had audit entries or events, they'd be created here
        }
    }
}
