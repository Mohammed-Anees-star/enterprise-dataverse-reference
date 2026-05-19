using EnterpriseTicketing.Application.Common.Exceptions;
using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Application.Tickets.Commands.CloseTicket;

public sealed class CloseTicketCommandHandler(
    ITicketRepository ticketRepository,
    IEventBus eventBus,
    ICurrentUserService currentUserService,
    ILogger<CloseTicketCommandHandler> logger) : IRequestHandler<CloseTicketCommand>
{
    public async Task Handle(CloseTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetByIdAsync(request.TicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Ticket), request.TicketId);

        var userId = currentUserService.UserId ?? "system";
        ticket.Close(userId);

        await ticketRepository.UpdateAsync(ticket, cancellationToken);

        foreach (var domainEvent in ticket.DomainEvents)
            await eventBus.PublishAsync(domainEvent, cancellationToken);

        ticket.ClearDomainEvents();

        logger.LogInformation("Ticket {TicketId} closed by user {UserId}", ticket.Id, userId);
    }
}
