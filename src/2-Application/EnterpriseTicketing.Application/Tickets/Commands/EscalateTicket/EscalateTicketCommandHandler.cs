using EnterpriseTicketing.Application.Common.Exceptions;
using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Application.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandler(
    ITicketRepository ticketRepository,
    IEventBus eventBus,
    ICurrentUserService currentUserService,
    ILogger<EscalateTicketCommandHandler> logger) : IRequestHandler<EscalateTicketCommand>
{
    public async Task Handle(EscalateTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetByIdAsync(request.TicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Ticket), request.TicketId);

        var userId = currentUserService.UserId ?? "system";
        ticket.Escalate(request.Reason, userId);

        await ticketRepository.UpdateAsync(ticket, cancellationToken);

        foreach (var domainEvent in ticket.DomainEvents)
            await eventBus.PublishAsync(domainEvent, cancellationToken);

        ticket.ClearDomainEvents();

        logger.LogWarning(
            "Ticket {TicketId} escalated to level {Level} by {UserId}. Reason: {Reason}",
            ticket.Id, ticket.EscalationCount, userId, request.Reason);
    }
}
