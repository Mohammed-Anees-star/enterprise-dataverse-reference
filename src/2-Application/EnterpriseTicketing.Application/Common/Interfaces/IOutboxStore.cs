using EnterpriseTicketing.Domain.Events;

namespace EnterpriseTicketing.Application.Common.Interfaces;

/// <summary>
/// Transactional outbox store.
///
/// Problem being solved:
///   Without an outbox, publishing a domain event to Service Bus after a
///   successful Dataverse write is still a dual-write:
///   — Dataverse write succeeds → Service Bus publish crashes → event is lost.
///   — Service Bus publish succeeds → process crashes before Dataverse write → duplicate.
///
/// Solution (Outbox Pattern):
///   1. Write the domain event to a <c>new_outboxevent</c> Dataverse table
///      in the same logical unit-of-work as the ticket create/update.
///   2. A background relay reads unpublished outbox records, publishes to Service Bus,
///      and marks them as published.
///
/// This guarantees at-least-once delivery to Service Bus with no event loss.
/// Idempotency on the consumer side (MessageId dedup) prevents double-processing.
///
/// Dataverse table: <c>new_outboxevent</c>
///   new_outboxeventid  PK
///   new_eventtype      String(200)
///   new_eventbody      Memo (JSON)
///   new_eventid        String(50)  — IDomainEvent.EventId as string
///   new_occurredat     DateTimeUtc
///   new_publishedat    DateTimeUtc (null = not yet published)
///   new_retrycount     Integer
/// </summary>
public interface IOutboxStore
{
    /// <summary>Appends a domain event to the outbox. Called from command handlers.</summary>
    Task AppendAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns unpublished outbox records in ascending <c>new_occurredat</c> order.
    /// Called by <see cref="OutboxRelayBackgroundService"/>.
    /// </summary>
    Task<IReadOnlyList<OutboxEntry>> GetUnpublishedAsync(
        int batchSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>Marks the outbox record as successfully published.</summary>
    Task MarkPublishedAsync(Guid outboxEntryId, CancellationToken cancellationToken = default);

    /// <summary>Increments the retry count; the relay will re-attempt on the next poll.</summary>
    Task IncrementRetryCountAsync(Guid outboxEntryId, CancellationToken cancellationToken = default);
}

/// <summary>Represents a single unpublished outbox record.</summary>
public sealed record OutboxEntry
{
    public required Guid OutboxEntryId { get; init; }
    public required string EventType { get; init; }
    public required string EventBody { get; init; }
    public required Guid EventId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required int RetryCount { get; init; }
}
