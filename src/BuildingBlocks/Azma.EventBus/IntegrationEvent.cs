namespace Azma.EventBus;

/// <summary>
/// Marker interface for cross-service integration events published over the message bus.
/// All concrete events must be records and live under <c>Azma.EventBus.Contracts.*</c>.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
}

public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
