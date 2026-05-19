using EnterpriseTicketing.Application.Tickets.Queries.GetTickets;
using MediatR;

namespace EnterpriseTicketing.Application.Tickets.Queries.GetTicketsByCustomer;

/// <summary>
/// Returns all tickets associated with a customer. Bypasses the generic filter for the
/// common "customer service rep opens a customer record" workflow.
/// </summary>
public sealed record GetTicketsByCustomerQuery : IRequest<IReadOnlyList<TicketSummaryDto>>
{
    public required Guid CustomerId { get; init; }
}
