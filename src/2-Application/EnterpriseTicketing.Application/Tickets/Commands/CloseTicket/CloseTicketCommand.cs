using MediatR;

namespace EnterpriseTicketing.Application.Tickets.Commands.CloseTicket;

public sealed record CloseTicketCommand : IRequest
{
    public required Guid TicketId { get; init; }
}
