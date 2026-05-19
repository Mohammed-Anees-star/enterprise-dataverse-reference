namespace EnterpriseTicketing.Domain.Enums;

/// <summary>
/// Ticket priority determines SLA, routing, and escalation behavior.
/// Critical tickets bypass standard queues and trigger immediate notifications.
/// </summary>
public enum TicketPriority
{
    Low = 100000000,
    Medium = 100000001,
    High = 100000002,
    Critical = 100000003
}
