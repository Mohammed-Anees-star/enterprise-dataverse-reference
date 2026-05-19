namespace EnterpriseTicketing.Domain.Exceptions;

public sealed class TicketNotFoundException : DomainException
{
    public TicketNotFoundException(Guid ticketId)
        : base($"Ticket with ID '{ticketId}' was not found.", "TICKET_NOT_FOUND")
    {
    }

    public TicketNotFoundException(string ticketNumber)
        : base($"Ticket '{ticketNumber}' was not found.", "TICKET_NOT_FOUND")
    {
    }
}
