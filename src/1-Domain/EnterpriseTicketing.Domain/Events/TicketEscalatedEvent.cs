namespace EnterpriseTicketing.Domain.Events;

public sealed record TicketEscalatedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(TicketEscalatedEvent);

    public required Guid TicketId { get; init; }
    public required string TicketNumber { get; init; }
    public required int EscalationLevel { get; init; }
    public required string Reason { get; init; }
    public required string EscalatedByUserId { get; init; }
}
