namespace EnterpriseTicketing.API.Models.Responses;

/// <summary>
/// Wire-format response for a ticket. Strings rather than enums to keep the contract
/// stable across versions and friendly to clients that don't share our C# enum.
/// </summary>
public sealed record TicketResponse
{
    public required Guid Id { get; init; }
    public required string TicketNumber { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Status { get; init; }
    public required string Priority { get; init; }
    public required string Category { get; init; }
    public required Guid CustomerId { get; init; }
    public string? AssignedToUserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public required int EscalationCount { get; init; }
    public string? ResolutionNotes { get; init; }
}
