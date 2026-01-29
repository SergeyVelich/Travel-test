namespace Travel.WebApi.Models;

public class CreateOrderRequest
{
    public int CustomerId { get; set; }
    public List<OrderItemRequest> Items { get; set; } = [];
    public decimal TotalAmount { get; set; }
}

public class OrderItemRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
