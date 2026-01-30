namespace Travel.WebApi.WebApi.Models;

public class CreateOrderRequest
{
    public int CustomerId { get; set; }
    public List<OrderItemRequest> Items { get; set; } = [];
}

public class OrderItemRequest
{
    public int Id { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}
