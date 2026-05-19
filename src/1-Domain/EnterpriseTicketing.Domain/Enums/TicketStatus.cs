namespace EnterpriseTicketing.Domain.Enums;

/// <summary>
/// Represents the lifecycle state of a support ticket.
/// Each state transition is governed by domain rules enforced in the Ticket entity.
/// Maps to Dataverse OptionSet values (100000000 series) to avoid conflicts with system values.
/// </summary>
public enum TicketStatus
{
    Open = 100000000,
    InProgress = 100000001,
    PendingCustomer = 100000002,
    PendingThirdParty = 100000003,
    Resolved = 100000004,
    Closed = 100000005,
    Cancelled = 100000006
}
