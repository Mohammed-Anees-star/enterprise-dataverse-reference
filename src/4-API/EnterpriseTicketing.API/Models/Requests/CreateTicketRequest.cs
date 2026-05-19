using EnterpriseTicketing.Domain.Enums;

namespace EnterpriseTicketing.API.Models.Requests;

/// <summary>
/// API request model for creating a ticket.
/// Separate from the Application command to allow independent versioning.
/// The API layer maps requests → commands, decoupling the API contract from application logic.
/// </summary>
public sealed record CreateTicketRequest
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required TicketPriority Priority { get; init; }
    public required TicketCategory Category { get; init; }
    public required Guid CustomerId { get; init; }
    public string? AssignedToUserId { get; init; }
}
