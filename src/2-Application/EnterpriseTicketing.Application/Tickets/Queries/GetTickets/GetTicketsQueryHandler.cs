using EnterpriseTicketing.Application.Common.Models;
using EnterpriseTicketing.Domain.Interfaces;
using MediatR;

namespace EnterpriseTicketing.Application.Tickets.Queries.GetTickets;

public sealed class GetTicketsQueryHandler(
    ITicketRepository ticketRepository) : IRequestHandler<GetTicketsQuery, PaginatedList<TicketSummaryDto>>
{
    public async Task<PaginatedList<TicketSummaryDto>> Handle(
        GetTicketsQuery request, CancellationToken cancellationToken)
    {
        var filter = new TicketFilter
        {
            Status = request.Status,
            Priority = request.Priority,
            Category = request.Category,
            CustomerId = request.CustomerId,
            AssignedToUserId = request.AssignedToUserId,
            SearchTerm = request.SearchTerm,
            SortBy = request.SortBy,
            SortDescending = request.SortDescending
        };

        var (tickets, totalCount) = await ticketRepository.GetPagedAsync(
            filter, request.PageNumber, request.PageSize, cancellationToken);

        var dtos = tickets.Select(t => new TicketSummaryDto
        {
            Id = t.Id,
            TicketNumber = t.TicketNumber.Value,
            Title = t.Title,
            Status = t.Status.ToString(),
            Priority = t.Priority.ToString(),
            Category = t.Category.ToString(),
            CustomerId = t.CustomerId,
            AssignedToUserId = t.AssignedToUserId,
            CreatedAt = t.CreatedAt,
            EscalationCount = t.EscalationCount
        }).ToList();

        return PaginatedList<TicketSummaryDto>.Create(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
