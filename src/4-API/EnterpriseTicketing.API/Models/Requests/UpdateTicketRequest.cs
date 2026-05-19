using EnterpriseTicketing.Domain.Enums;

namespace EnterpriseTicketing.API.Models.Requests;

public sealed record UpdateTicketRequest
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required TicketPriority Priority { get; init; }
    public required TicketCategory Category { get; init; }
}
