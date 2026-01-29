namespace Travel.WebApi.WebApi.Endpoints.Orders.Models;

public class CreateOrderRequest
{
    public long CustomerId { get; set; }
    public List<OrderItemRequest> Items { get; set; } = [];
}

public class OrderItemRequest
{
    public long Id { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}
