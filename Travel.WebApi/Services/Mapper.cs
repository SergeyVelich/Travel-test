using System;
using Travel.WebApi.Domain;
using Travel.WebApi.Models;

namespace Travel.WebApi.Services;

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
            TotalAmount = request.TotalAmount,
            Status = OrderStatuses.New
        };

        if (request.Items is not null)
        {
            foreach (var item in request.Items)
            {
                entity.Items.Add(ToDomainModel(item));
            }
        }

        return entity;
    }

    public Item ToDomainModel(OrderItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = new Item
        {
            Name = request.Name,
            Price = request.Price
        };

        return entity;
    }
}