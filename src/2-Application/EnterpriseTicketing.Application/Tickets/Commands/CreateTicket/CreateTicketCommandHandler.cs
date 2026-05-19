using EnterpriseTicketing.Application.Common.Exceptions;
using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Domain.Entities;
using EnterpriseTicketing.Domain.Interfaces;
using EnterpriseTicketing.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Application.Tickets.Commands.CreateTicket;

/// <summary>
/// Handles the CreateTicketCommand — orchestrates domain logic and infrastructure concerns.
///
/// Handler responsibilities:
///   1. Validate pre-conditions (customer exists)
///   2. Generate ticket number (could use a sequence service in production)
///   3. Create domain entity via factory method (enforces invariants)
///   4. Persist via repository
///   5. Dispatch domain events via event bus
///   6. Return the new ticket ID
///
/// The handler does NOT know about HTTP, Dataverse tables, or Service Bus.
/// It only speaks in domain terms through abstractions.
/// </summary>
public sealed class CreateTicketCommandHandler(
    ITicketRepository ticketRepository,
    ICustomerRepository customerRepository,
    IEventBus eventBus,
    ILogger<CreateTicketCommandHandler> logger) : IRequestHandler<CreateTicketCommand, Guid>
{
    public async Task<Guid> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
    {
        // Verify customer exists before creating ticket
        var customerExists = await customerRepository.ExistsAsync(request.CustomerId, cancellationToken);
        if (!customerExists)
            throw new NotFoundException(nameof(Domain.Entities.Customer), request.CustomerId);

        // Generate ticket number — in production, use an atomic sequence (Redis INCR or DB sequence)
        var ticketNumber = TicketNumber.Create(DateTimeOffset.UtcNow.Year, new Random().Next(1, 999999));

        // Create via domain factory — all invariants are enforced here
        var ticket = Ticket.Create(
            ticketNumber,
            request.Title,
            request.Description,
            request.Priority,
            request.Category,
            request.CustomerId,
            request.AssignedToUserId);

        await ticketRepository.AddAsync(ticket, cancellationToken);

        logger.LogInformation(
            "Ticket {TicketNumber} created with ID {TicketId} for customer {CustomerId}",
            ticket.TicketNumber.Value, ticket.Id, ticket.CustomerId);

        // Publish domain events after successful persistence (avoids dual-write problem)
        foreach (var domainEvent in ticket.DomainEvents)
        {
            await eventBus.PublishAsync(domainEvent, cancellationToken);
        }

        ticket.ClearDomainEvents();
        return ticket.Id;
    }
}
