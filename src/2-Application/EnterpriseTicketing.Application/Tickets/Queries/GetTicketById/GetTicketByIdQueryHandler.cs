using EnterpriseTicketing.Application.Common.Exceptions;
using EnterpriseTicketing.Domain.Interfaces;
using MediatR;

namespace EnterpriseTicketing.Application.Tickets.Queries.GetTicketById;

public sealed class GetTicketByIdQueryHandler(
    ITicketRepository ticketRepository) : IRequestHandler<GetTicketByIdQuery, TicketDetailDto>
{
    public async Task<TicketDetailDto> Handle(GetTicketByIdQuery request, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetByIdAsync(request.TicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Ticket), request.TicketId);

        return new TicketDetailDto
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber.Value,
            Title = ticket.Title,
            Description = ticket.Description,
            Status = ticket.Status.ToString(),
            Priority = ticket.Priority.ToString(),
            Category = ticket.Category.ToString(),
            CustomerId = ticket.CustomerId,
            AssignedToUserId = ticket.AssignedToUserId,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            ResolvedAt = ticket.ResolvedAt,
            ClosedAt = ticket.ClosedAt,
            EscalationCount = ticket.EscalationCount,
            ResolutionNotes = ticket.ResolutionNotes
        };
    }
}
