namespace Travel.WebApi.Domain.Entities;

public class ProcessedMessage
{
    public long Id { get; set; }
    public long EntityId { get; set; }
    public string MessageId { get; set; } = null!;
    public DateTime ProcessedAt { get; set; }
    public string MessageType { get; set; } = null!;
}
