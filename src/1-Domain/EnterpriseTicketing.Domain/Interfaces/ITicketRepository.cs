using EnterpriseTicketing.Domain.Entities;
using EnterpriseTicketing.Domain.Enums;

namespace EnterpriseTicketing.Domain.Interfaces;

/// <summary>
/// Domain repository interface — defined in the Domain layer, implemented in Infrastructure.
/// This is the Dependency Inversion Principle in action:
///   - Domain declares what it needs (abstraction)
///   - Infrastructure provides how it works (implementation)
///   - Application orchestrates via the abstraction
///
/// The domain has zero knowledge of Dataverse, SQL, or any storage technology.
/// This enables testing without infrastructure and swapping storage providers.
/// </summary>
public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Ticket?> GetByTicketNumberAsync(string ticketNumber, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Ticket> Items, int TotalCount)> GetPagedAsync(
        TicketFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Ticket>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default);
    Task UpdateAsync(Ticket ticket, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Filter parameters for ticket queries.
/// Using a dedicated filter object is cleaner than long method parameter lists
/// and makes future additions (e.g., date range filters) non-breaking.
/// </summary>
public sealed record TicketFilter
{
    public TicketStatus? Status { get; init; }
    public TicketPriority? Priority { get; init; }
    public TicketCategory? Category { get; init; }
    public Guid? CustomerId { get; init; }
    public string? AssignedToUserId { get; init; }
    public string? SearchTerm { get; init; }
    public DateTimeOffset? CreatedAfter { get; init; }
    public DateTimeOffset? CreatedBefore { get; init; }
    public string SortBy { get; init; } = "CreatedAt";
    public bool SortDescending { get; init; } = true;
}
