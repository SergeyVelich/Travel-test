namespace Travel.WebApi.Pubsub.Messages;

public sealed record StartOrderSubmitEvent
{
    public required int OrderId { get; init; }
    public DateTime EnqueuedAt { get; init; } = DateTime.UtcNow;
    public string? CorrelationId { get; init; }
}