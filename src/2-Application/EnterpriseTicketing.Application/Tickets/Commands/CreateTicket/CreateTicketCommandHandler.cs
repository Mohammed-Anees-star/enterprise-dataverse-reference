using EnterpriseTicketing.Application.Common.Exceptions;
using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Domain.Entities;
using EnterpriseTicketing.Domain.Interfaces;
using EnterpriseTicketing.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Application.Tickets.Commands.CreateTicket;

/// <summary>
/// Handles <see cref="CreateTicketCommand"/>.
///
/// Two production-safety improvements over the naïve implementation:
///
/// 1. Atomic ticket number via <see cref="ITicketNumberSequence"/>
///    — eliminates the <c>new Random().Next()</c> race that could produce duplicates
///      under concurrent load.
///
/// 2. Outbox pattern via <see cref="IOutboxStore"/>
///    — domain events are written to Dataverse in the same logical operation as the
///      ticket itself.  The <c>OutboxRelayBackgroundService</c> reads and forwards them
///      to Service Bus asynchronously, preventing event loss on crash/restart.
/// </summary>
public sealed class CreateTicketCommandHandler(
    ITicketRepository ticketRepository,
    ICustomerRepository customerRepository,
    IOutboxStore outboxStore,
    ITicketNumberSequence ticketNumberSequence,
    ILogger<CreateTicketCommandHandler> logger) : IRequestHandler<CreateTicketCommand, Guid>
{
    public async Task<Guid> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
    {
        var customerExists = await customerRepository.ExistsAsync(request.CustomerId, cancellationToken);
        if (!customerExists)
            throw new NotFoundException(nameof(Customer), request.CustomerId);

        var year = DateTimeOffset.UtcNow.Year;
        var sequence = await ticketNumberSequence.NextAsync(year, cancellationToken);
        var ticketNumber = TicketNumber.Create(year, sequence);

        var ticket = Ticket.Create(
            ticketNumber,
            request.Title,
            request.Description,
            request.Priority,
            request.Category,
            request.CustomerId,
            request.AssignedToUserId);

        // Persist ticket to Dataverse
        await ticketRepository.AddAsync(ticket, cancellationToken);

        logger.LogInformation(
            "Ticket {TicketNumber} created (Id={TicketId}, Customer={CustomerId})",
            ticket.TicketNumber.Value, ticket.Id, ticket.CustomerId);

        // Append domain events to outbox — written to Dataverse, relayed to Service Bus
        // by OutboxRelayBackgroundService. Survives process restarts; no dual-write risk.
        foreach (var domainEvent in ticket.DomainEvents)
            await outboxStore.AppendAsync(domainEvent, cancellationToken);

        ticket.ClearDomainEvents();
        return ticket.Id;
    }
}
