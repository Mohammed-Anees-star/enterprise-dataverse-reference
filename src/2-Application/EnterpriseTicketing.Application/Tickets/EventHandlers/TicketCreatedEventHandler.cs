using EnterpriseTicketing.Domain.Events;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Application.Tickets.EventHandlers;

/// <summary>
/// Synchronous in-process handler for <see cref="TicketCreatedEvent"/>.
///
/// With the Outbox pattern the primary delivery path is:
///   Command Handler → IOutboxStore.AppendAsync → OutboxRelayBackgroundService → Service Bus
///
/// This class handles only the lightweight in-process side-effects that must
/// complete in the same request (metrics, cache invalidation, audit ring buffer).
/// It is invoked by the command handler after a successful IOutboxStore.AppendAsync.
///
/// NOT a MediatR INotificationHandler — domain events are not dispatched via MediatR;
/// they are appended to the outbox and dispatched asynchronously via Service Bus.
/// </summary>
public sealed class TicketCreatedEventHandler(ILogger<TicketCreatedEventHandler> logger)
{
    public Task HandleAsync(TicketCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "In-process: ticket {TicketNumber} created (Id={TicketId}, Customer={CustomerId}, Priority={Priority})",
            @event.TicketNumber, @event.TicketId, @event.CustomerId, @event.Priority);

        // Hook points for inline side-effects:
        //   - Increment Application Insights custom metric "tickets_created_total"
        //   - Invalidate per-customer ticket-list cache entry
        return Task.CompletedTask;
    }
}
