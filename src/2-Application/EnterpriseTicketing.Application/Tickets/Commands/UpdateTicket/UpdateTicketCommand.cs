using EnterpriseTicketing.Domain.Enums;
using MediatR;

namespace EnterpriseTicketing.Application.Tickets.Commands.UpdateTicket;

public sealed record UpdateTicketCommand : IRequest
{
    public required Guid TicketId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required TicketPriority Priority { get; init; }
    public required TicketCategory Category { get; init; }
}
