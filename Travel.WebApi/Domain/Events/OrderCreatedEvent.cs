namespace Travel.WebApi.Domain.Events;

public class OrderCreatedEvent
{
    public long OrderId { get; set; }
    public string? CorrelationId { get; set; }
}
