namespace Travel.WebApi.Domain;

public class Inventory
{
    public int ItemId { get; set; }
    public Item Item { get; set; }

    public int InStock { get; set; }
    public int Reserved { get; set; }
    public int InTransit { get; set; }

    public int Available => InStock - Reserved;
}
