using EnterpriseTicketing.Domain.Enums;

namespace EnterpriseTicketing.Domain.Exceptions;

public sealed class InvalidTicketStateException : DomainException
{
    public InvalidTicketStateException(Guid ticketId, TicketStatus currentStatus, string attemptedOperation)
        : base(
            $"Cannot perform '{attemptedOperation}' on ticket '{ticketId}' in status '{currentStatus}'.",
            "INVALID_TICKET_STATE")
    {
    }
}
