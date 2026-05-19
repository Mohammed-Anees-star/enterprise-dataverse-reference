using EnterpriseTicketing.Application.Tickets.Queries.GetTickets;
using EnterpriseTicketing.Domain.Interfaces;
using MediatR;

namespace EnterpriseTicketing.Application.Tickets.Queries.GetTicketsByCustomer;

public sealed class GetTicketsByCustomerQueryHandler(ITicketRepository ticketRepository)
    : IRequestHandler<GetTicketsByCustomerQuery, IReadOnlyList<TicketSummaryDto>>
{
    public async Task<IReadOnlyList<TicketSummaryDto>> Handle(
        GetTicketsByCustomerQuery request,
        CancellationToken cancellationToken)
    {
        var tickets = await ticketRepository
            .GetByCustomerIdAsync(request.CustomerId, cancellationToken)
            .ConfigureAwait(false);

        return tickets.Select(t => new TicketSummaryDto
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
    }
}
