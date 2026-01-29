namespace Travel.WebApi.Domain.Entities;

public class Item
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
    public Inventory? Inventory { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = [];
}
