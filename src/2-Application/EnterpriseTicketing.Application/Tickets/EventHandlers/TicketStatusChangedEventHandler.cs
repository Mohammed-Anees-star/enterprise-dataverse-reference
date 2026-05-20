using EnterpriseTicketing.Domain.Enums;
using EnterpriseTicketing.Domain.Events;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Application.Tickets.EventHandlers;

/// <summary>
/// In-process handler for ticket status transitions. Lightweight bookkeeping only —
/// any outbound I/O (customer email, Teams notification) is handled by the
/// durable Service Bus consumer (<c>TicketProcessingBackgroundService</c>).
/// </summary>
public sealed class TicketStatusChangedEventHandler(ILogger<TicketStatusChangedEventHandler> logger)
{
    public Task HandleAsync(TicketStatusChangedEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "StatusChanged: {TicketNumber} {OldStatus} → {NewStatus} by {User}",
            @event.TicketNumber, @event.OldStatus, @event.NewStatus, @event.ChangedByUserId);

        if (@event.NewStatus is TicketStatus.Resolved or TicketStatus.Closed)
            logger.LogDebug("SLA timer stopped for ticket {TicketId}", @event.TicketId);

        return Task.CompletedTask;
    }
}
