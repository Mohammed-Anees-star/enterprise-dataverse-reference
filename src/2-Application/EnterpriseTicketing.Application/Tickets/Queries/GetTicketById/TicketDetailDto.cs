namespace EnterpriseTicketing.Application.Tickets.Queries.GetTicketById;

/// <summary>
/// Data Transfer Object for ticket detail view.
/// DTOs live in the Application layer — they decouple the API contract from the domain model.
/// This allows the domain to evolve without breaking API consumers.
/// String representations of enums are used for serialization clarity.
/// </summary>
public sealed record TicketDetailDto
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
