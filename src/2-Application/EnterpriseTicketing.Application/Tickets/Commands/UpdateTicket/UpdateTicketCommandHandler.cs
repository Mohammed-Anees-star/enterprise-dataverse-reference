using EnterpriseTicketing.Application.Common.Exceptions;
using EnterpriseTicketing.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Application.Tickets.Commands.UpdateTicket;

public sealed class UpdateTicketCommandHandler(
    ITicketRepository ticketRepository,
    ILogger<UpdateTicketCommandHandler> logger) : IRequestHandler<UpdateTicketCommand>
{
    public async Task Handle(UpdateTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetByIdAsync(request.TicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Ticket), request.TicketId);

        ticket.UpdateDetails(request.Title, request.Description, request.Priority, request.Category);

        await ticketRepository.UpdateAsync(ticket, cancellationToken);

        logger.LogInformation("Ticket {TicketId} updated", ticket.Id);
    }
}
