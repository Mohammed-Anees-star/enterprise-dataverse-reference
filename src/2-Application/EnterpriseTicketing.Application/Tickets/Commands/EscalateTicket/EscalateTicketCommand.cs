using MediatR;

namespace EnterpriseTicketing.Application.Tickets.Commands.EscalateTicket;

public sealed record EscalateTicketCommand : IRequest
{
    public required Guid TicketId { get; init; }
    public required string Reason { get; init; }
}
