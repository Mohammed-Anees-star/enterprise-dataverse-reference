using EnterpriseTicketing.Domain.Enums;
using MediatR;

namespace EnterpriseTicketing.Application.Tickets.Commands.CreateTicket;

/// <summary>
/// Command to create a new support ticket.
/// Commands are immutable records — they represent intent, not state.
/// Using records ensures value-based equality, which simplifies testing and deduplication.
/// </summary>
public sealed record CreateTicketCommand : IRequest<Guid>
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required TicketPriority Priority { get; init; }
    public required TicketCategory Category { get; init; }
    public required Guid CustomerId { get; init; }
    public string? AssignedToUserId { get; init; }
}
