using MediatR;

namespace EnterpriseTicketing.Application.Tickets.Queries.GetTicketById;

public sealed record GetTicketByIdQuery(Guid TicketId) : IRequest<TicketDetailDto>;
