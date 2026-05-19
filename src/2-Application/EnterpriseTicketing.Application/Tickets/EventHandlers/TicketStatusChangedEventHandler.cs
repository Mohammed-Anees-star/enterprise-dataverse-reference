using EnterpriseTicketing.Domain.Enums;
using EnterpriseTicketing.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Application.Tickets.EventHandlers;

/// <summary>
/// In-process handler for ticket status transitions. Lightweight bookkeeping only -
/// any outbound IO (e.g., customer email) is performed by the durable Service Bus consumer.
/// </summary>
public sealed class TicketStatusChangedEventHandler(ILogger<TicketStatusChangedEventHandler> logger)
    : INotificationHandler<TicketStatusChangedEvent>
{
    public Task Handle(TicketStatusChangedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Domain event: ticket {TicketNumber} transitioned {OldStatus} -> {NewStatus} by {User}",
            notification.TicketNumber, notification.OldStatus, notification.NewStatus, notification.ChangedByUserId);

        // SLA timers are stopped here when entering a terminal state - in production
        // this would update a Redis-backed sorted set keyed by ticket id.
        if (notification.NewStatus is TicketStatus.Resolved or TicketStatus.Closed)
        {
            logger.LogDebug("SLA timer stopped for ticket {TicketId}", notification.TicketId);
        }

        return Task.CompletedTask;
    }
}
