using EnterpriseTicketing.Domain.Enums;

namespace EnterpriseTicketing.Domain.Events;

public sealed record TicketStatusChangedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(TicketStatusChangedEvent);

    public required Guid TicketId { get; init; }
    public required string TicketNumber { get; init; }
    public required TicketStatus OldStatus { get; init; }
    public required TicketStatus NewStatus { get; init; }
    public required string ChangedByUserId { get; init; }
}
