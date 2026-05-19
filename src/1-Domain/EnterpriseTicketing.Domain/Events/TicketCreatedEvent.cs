using EnterpriseTicketing.Domain.Enums;

namespace EnterpriseTicketing.Domain.Events;

/// <summary>
/// Raised when a ticket is successfully created.
/// Downstream handlers use this to: send customer confirmation emails,
/// trigger SLA timers, notify assigned agents, publish to Service Bus.
/// </summary>
public sealed record TicketCreatedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(TicketCreatedEvent);

    public required Guid TicketId { get; init; }
    public required string TicketNumber { get; init; }
    public required Guid CustomerId { get; init; }
    public required TicketPriority Priority { get; init; }
    public required TicketCategory Category { get; init; }
    public required string Title { get; init; }
}
