namespace EnterpriseTicketing.Application.Tickets.Queries.GetTickets;

public sealed record TicketSummaryDto
{
    public required Guid Id { get; init; }
    public required string TicketNumber { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public required string Priority { get; init; }
    public required string Category { get; init; }
    public required Guid CustomerId { get; init; }
    public string? AssignedToUserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required int EscalationCount { get; init; }
}
