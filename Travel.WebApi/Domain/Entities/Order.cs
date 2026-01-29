namespace Travel.WebApi.Domain.Entities;

public class Order
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatuses Status { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];
}