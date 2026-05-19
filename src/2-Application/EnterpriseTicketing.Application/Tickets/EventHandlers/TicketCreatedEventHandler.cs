using EnterpriseTicketing.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Application.Tickets.EventHandlers;

/// <summary>
/// In-process domain event handler for TicketCreatedEvent.
/// Runs side-effects that must complete within the request (e.g., audit log entries,
/// in-memory cache invalidation). Heavy work (email, downstream system writes)
/// is instead handled by the out-of-process Service Bus consumers in Milestone 3+.
/// </summary>
public sealed class TicketCreatedEventHandler(ILogger<TicketCreatedEventHandler> logger)
    : INotificationHandler<TicketCreatedEvent>
{
    public Task Handle(TicketCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Domain event: ticket {TicketNumber} created (id={TicketId}, customer={CustomerId}, priority={Priority})",
            notification.TicketNumber, notification.TicketId, notification.CustomerId, notification.Priority);

        // Production hook points (kept thin on purpose - heavy lifting belongs in the
        // out-of-process consumer, not the request thread):
        //   - Increment "tickets_created_total" Application Insights custom metric
        //   - Invalidate per-customer ticket-list cache entry
        //   - Append a row to the in-memory audit ring buffer flushed by a hosted service

        return Task.CompletedTask;
    }
}
