using Travel.WebApi.Domain.Entities;
using Travel.WebApi.WebApi.Endpoints.Orders.Models;

namespace Travel.WebApi.WebApi.Mapping;

public interface IMapper
{
    Order ToDomainModel(CreateOrderRequest request);
}

public class Mapper : IMapper
{
    public Order ToDomainModel(CreateOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = new Order
        {
            CustomerId = request.CustomerId,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatuses.New
        };

        if (request.Items is not null)
        {
            foreach (var item in request.Items)
            {
                entity.Items.Add(ToDomainModel(item));
            }
        }

        entity.TotalAmount = entity.Items.Sum(i => i.Price * i.Quantity);

        return entity;
    }

    public OrderItem ToDomainModel(OrderItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = new OrderItem
        {
            ItemId = request.Id,
            Price = request.Price,
            Quantity = request.Quantity
        };

        return entity;
    }
}