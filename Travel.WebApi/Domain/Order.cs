namespace Travel.WebApi.Domain;

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatuses Status { get; set; }

    public ICollection<Item> Items { get; set; } = [];
}