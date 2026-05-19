using EnterpriseTicketing.Application.Common.Models;
using EnterpriseTicketing.Domain.Enums;
using MediatR;

namespace EnterpriseTicketing.Application.Tickets.Queries.GetTickets;

public sealed record GetTicketsQuery : IRequest<PaginatedList<TicketSummaryDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public TicketStatus? Status { get; init; }
    public TicketPriority? Priority { get; init; }
    public TicketCategory? Category { get; init; }
    public Guid? CustomerId { get; init; }
    public string? AssignedToUserId { get; init; }
    public string? SearchTerm { get; init; }
    public string SortBy { get; init; } = "CreatedAt";
    public bool SortDescending { get; init; } = true;
}
