namespace EnterpriseTicketing.Domain.Events;

/// <summary>
/// Marker interface for domain events.
/// Domain events represent something meaningful that happened in the domain.
/// They are raised by domain entities and dispatched after persistence to avoid
/// the dual-write problem (event bus + database in same transaction).
///
/// Enterprise pattern: collect events on the entity during command processing,
/// dispatch via IEventBus after SaveChanges succeeds.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
    string EventType { get; }
}
