using EnterpriseTicketing.Domain.Entities;
using EnterpriseTicketing.Domain.Enums;
using EnterpriseTicketing.Domain.ValueObjects;

namespace EnterpriseTicketing.Infrastructure.Dataverse.Mapping;

/// <summary>
/// Translates between the Ticket domain entity and the Dataverse attribute dictionary.
///
/// Keeping mapping logic out of the repository keeps the repo focused on persistence
/// orchestration and makes the column-name knowledge testable in isolation.
///
/// Column map (logical names on the new_ticket table):
///   new_ticketid        - PK (Guid)
///   new_ticketnumber    - String, ticket number (TKT-YYYY-NNNNNN)
///   new_title           - String
///   new_description     - Memo
///   new_status          - OptionSet (TicketStatus values)
///   new_priority        - OptionSet (TicketPriority values)
///   new_category        - OptionSet (TicketCategory values)
///   new_customerid      - Lookup to new_customer
///   new_assignedtouserid - String
///   new_resolvedat      - DateTime
///   new_closedat        - DateTime
///   new_escalationcount - Integer
///   createdon           - System DateTime
///   modifiedon          - System DateTime
/// </summary>
public static class TicketMapper
{
    public const string EntityLogicalName = "new_ticket";
    public const string EntitySetName = "new_tickets";

    public const string IdColumn = "new_ticketid";
    public const string TicketNumberColumn = "new_ticketnumber";
    public const string TitleColumn = "new_title";
    public const string DescriptionColumn = "new_description";
    public const string StatusColumn = "new_status";
    public const string PriorityColumn = "new_priority";
    public const string CategoryColumn = "new_category";
    public const string CustomerIdColumn = "new_customerid";
    public const string AssignedToUserIdColumn = "new_assignedtouserid";
    public const string ResolvedAtColumn = "new_resolvedat";
    public const string ClosedAtColumn = "new_closedat";
    public const string EscalationCountColumn = "new_escalationcount";
    public const string CreatedAtColumn = "createdon";
    public const string UpdatedAtColumn = "modifiedon";

    public static IReadOnlyList<string> AllColumns =>
    [
        IdColumn, TicketNumberColumn, TitleColumn, DescriptionColumn, StatusColumn,
        PriorityColumn, CategoryColumn, CustomerIdColumn, AssignedToUserIdColumn,
        ResolvedAtColumn, ClosedAtColumn, EscalationCountColumn,
        CreatedAtColumn, UpdatedAtColumn
    ];

    public static Dictionary<string, object> ToDataverseAttributes(Ticket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var attributes = new Dictionary<string, object>
        {
            [TicketNumberColumn] = ticket.TicketNumber.Value,
            [TitleColumn] = ticket.Title,
            [DescriptionColumn] = ticket.Description,
            [StatusColumn] = (int)ticket.Status,
            [PriorityColumn] = (int)ticket.Priority,
            [CategoryColumn] = (int)ticket.Category,
            [CustomerIdColumn] = ticket.CustomerId,
            [EscalationCountColumn] = ticket.EscalationCount
        };

        if (!string.IsNullOrWhiteSpace(ticket.AssignedToUserId))
        {
            attributes[AssignedToUserIdColumn] = ticket.AssignedToUserId;
        }

        if (ticket.ResolvedAt.HasValue)
        {
            attributes[ResolvedAtColumn] = ticket.ResolvedAt.Value.UtcDateTime;
        }

        if (ticket.ClosedAt.HasValue)
        {
            attributes[ClosedAtColumn] = ticket.ClosedAt.Value.UtcDateTime;
        }

        return attributes;
    }

    public static Ticket FromDataverseAttributes(IReadOnlyDictionary<string, object?> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var id = GetGuid(attributes, IdColumn);
        var ticketNumber = TicketNumber.Parse(GetString(attributes, TicketNumberColumn));
        var status = (TicketStatus)GetInt(attributes, StatusColumn);
        var priority = (TicketPriority)GetInt(attributes, PriorityColumn);
        var category = (TicketCategory)GetInt(attributes, CategoryColumn);
        var customerId = GetGuid(attributes, CustomerIdColumn);

        return Ticket.Reconstitute(
            id: id,
            ticketNumber: ticketNumber,
            title: GetString(attributes, TitleColumn),
            description: GetString(attributes, DescriptionColumn),
            status: status,
            priority: priority,
            category: category,
            customerId: customerId,
            assignedToUserId: GetNullableString(attributes, AssignedToUserIdColumn),
            createdAt: GetDateTimeOffset(attributes, CreatedAtColumn),
            updatedAt: GetDateTimeOffset(attributes, UpdatedAtColumn),
            resolvedAt: GetNullableDateTimeOffset(attributes, ResolvedAtColumn),
            closedAt: GetNullableDateTimeOffset(attributes, ClosedAtColumn),
            escalationCount: GetInt(attributes, EscalationCountColumn),
            resolutionNotes: null);
    }

    private static string GetString(IReadOnlyDictionary<string, object?> a, string key) =>
        a.TryGetValue(key, out var v) && v is string s ? s : string.Empty;

    private static string? GetNullableString(IReadOnlyDictionary<string, object?> a, string key) =>
        a.TryGetValue(key, out var v) ? v as string : null;

    private static int GetInt(IReadOnlyDictionary<string, object?> a, string key)
    {
        if (!a.TryGetValue(key, out var v) || v is null) return 0;
        return v is int i ? i : Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static Guid GetGuid(IReadOnlyDictionary<string, object?> a, string key)
    {
        if (!a.TryGetValue(key, out var v) || v is null) return Guid.Empty;
        return v is Guid g ? g : Guid.Parse(v.ToString()!);
    }

    private static DateTimeOffset GetDateTimeOffset(IReadOnlyDictionary<string, object?> a, string key)
    {
        if (!a.TryGetValue(key, out var v) || v is null) return default;
        return v switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime()),
            string s => DateTimeOffset.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
            _ => default
        };
    }

    private static DateTimeOffset? GetNullableDateTimeOffset(IReadOnlyDictionary<string, object?> a, string key)
    {
        if (!a.TryGetValue(key, out var v) || v is null) return null;
        return GetDateTimeOffset(a, key);
    }
}
