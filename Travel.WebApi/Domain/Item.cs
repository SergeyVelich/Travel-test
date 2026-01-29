namespace Travel.WebApi.Domain;

public class Item
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
}
