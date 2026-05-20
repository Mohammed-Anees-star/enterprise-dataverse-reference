using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Domain.Entities;
using EnterpriseTicketing.Domain.Enums;
using EnterpriseTicketing.Domain.Interfaces;
using EnterpriseTicketing.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Infrastructure.Dataverse.Repositories;

/// <summary>
/// Dataverse-backed implementation of <see cref="ITicketRepository"/>.
///
/// Fixes applied:
///  1. Lookup columns (new_customerid) wrapped in <see cref="LookupValue"/> so the
///     SDK correctly produces <c>EntityReference</c> rather than a raw Guid.
///  2. Primary-key column (new_ticketid) passed as raw Guid — NOT LookupValue.
///  3. All string values that flow into FetchXML conditions are passed through
///     <see cref="DataverseService.XmlEscape"/> to prevent injection.
///  4. Pagination total count reads <c>TotalRecordCount</c> from the
///     <c>QueryEntitiesWithCountAsync</c> overload — accurate, not estimated.
/// </summary>
public sealed class TicketRepository : ITicketRepository
{
    private const string EntityName = "new_ticket";
    private const string CustomerEntityName = "new_customer";

    private readonly IDataverseService _dataverseService;
    private readonly ILogger<TicketRepository> _logger;

    private static readonly string[] AllColumns =
    [
        "new_ticketid", "new_ticketnumber", "new_title", "new_description",
        "new_status", "new_priority", "new_category", "new_customerid",
        "new_assignedtouserid", "createdon", "modifiedon",
        "new_resolvedat", "new_closedat", "new_escalationcount", "new_resolutionnotes"
    ];

    public TicketRepository(IDataverseService dataverseService, ILogger<TicketRepository> logger)
    {
        _dataverseService = dataverseService;
        _logger = logger;
    }

    public async Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var attrs = await _dataverseService.GetEntityAsync(EntityName, id, AllColumns, cancellationToken);
        return attrs is not null ? MapToTicket(attrs) : null;
    }

    public async Task<Ticket?> GetByTicketNumberAsync(
        string ticketNumber, CancellationToken cancellationToken = default)
    {
        // FIX: XML-escape the ticket number (user-supplied value)
        var escapedNumber = DataverseService.XmlEscape(ticketNumber);

        var fetchXml = $"""
            <fetch top="1">
              <entity name="{EntityName}">
                {AttributeColumns()}
                <filter>
                  <condition attribute="new_ticketnumber" operator="eq" value="{escapedNumber}" />
                </filter>
              </entity>
            </fetch>
            """;

        var (records, _) = await _dataverseService.QueryEntitiesAsync(EntityName, fetchXml, cancellationToken);
        return records.FirstOrDefault() is { } a ? MapToTicket(a) : null;
    }

    public async Task<(IReadOnlyList<Ticket> Items, int TotalCount)> GetPagedAsync(
        TicketFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var conditions = BuildFilterConditions(filter);
        var orderAttr = MapSortColumn(filter.SortBy);
        var descending = filter.SortDescending ? "true" : "false";

        var fetchXml = $"""
            <fetch count="{pageSize}" page="{pageNumber}" returntotalrecordcount="true">
              <entity name="{EntityName}">
                {AttributeColumns()}
                {(conditions.Count > 0
                    ? $"<filter type=\"and\">{string.Join(string.Empty, conditions)}</filter>"
                    : string.Empty)}
                <order attribute="{orderAttr}" descending="{descending}" />
              </entity>
            </fetch>
            """;

        // FIX: use the overload that returns the server-computed TotalRecordCount
        var (records, totalCount, _) = await _dataverseService.QueryEntitiesWithCountAsync(
            EntityName, fetchXml, cancellationToken);

        var tickets = records.Select(MapToTicket).ToList();
        return (tickets.AsReadOnly(), totalCount);
    }

    public async Task<IReadOnlyList<Ticket>> GetByCustomerIdAsync(
        Guid customerId, CancellationToken cancellationToken = default)
    {
        // Guid values are safe to embed directly in FetchXML; no escaping needed
        var fetchXml = $"""
            <fetch>
              <entity name="{EntityName}">
                {AttributeColumns()}
                <filter>
                  <condition attribute="new_customerid" operator="eq" value="{customerId}" />
                </filter>
                <order attribute="createdon" descending="true" />
              </entity>
            </fetch>
            """;

        var (records, _) = await _dataverseService.QueryEntitiesAsync(EntityName, fetchXml, cancellationToken);
        return records.Select(MapToTicket).ToList().AsReadOnly();
    }

    public async Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        var attrs = MapToAttributes(ticket);
        await _dataverseService.CreateEntityAsync(EntityName, attrs, cancellationToken);
        _logger.LogDebug("Persisted ticket {TicketNumber} ({TicketId})", ticket.TicketNumber, ticket.Id);
    }

    public async Task UpdateAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        var attrs = MapToAttributes(ticket);
        await _dataverseService.UpdateEntityAsync(EntityName, ticket.Id, attrs, cancellationToken);
        _logger.LogDebug("Updated ticket {TicketId}", ticket.Id);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => _dataverseService.DeleteEntityAsync(EntityName, id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => _dataverseService.ExistsAsync(EntityName, id, cancellationToken);

    // -------------------------------------------------------------------------
    // Mapping
    // -------------------------------------------------------------------------

    private static Ticket MapToTicket(Dictionary<string, object> attrs) =>
        Ticket.Reconstitute(
            id: GetGuid(attrs, "new_ticketid"),
            ticketNumber: TicketNumber.Parse(GetString(attrs, "new_ticketnumber")),
            title: GetString(attrs, "new_title"),
            description: GetString(attrs, "new_description"),
            status: (TicketStatus)GetInt(attrs, "new_status"),
            priority: (TicketPriority)GetInt(attrs, "new_priority"),
            category: (TicketCategory)GetInt(attrs, "new_category"),
            customerId: GetGuid(attrs, "new_customerid"),
            assignedToUserId: GetStringOrNull(attrs, "new_assignedtouserid"),
            createdAt: GetDateTimeOffset(attrs, "createdon"),
            updatedAt: GetDateTimeOffset(attrs, "modifiedon"),
            resolvedAt: GetDateTimeOffsetOrNull(attrs, "new_resolvedat"),
            closedAt: GetDateTimeOffsetOrNull(attrs, "new_closedat"),
            escalationCount: GetInt(attrs, "new_escalationcount"),
            resolutionNotes: GetStringOrNull(attrs, "new_resolutionnotes"));

    private static Dictionary<string, object> MapToAttributes(Ticket ticket)
    {
        var attrs = new Dictionary<string, object>
        {
            // Primary key — raw Guid, NOT LookupValue
            ["new_ticketid"]       = ticket.Id,
            ["new_ticketnumber"]   = ticket.TicketNumber.Value,
            ["new_title"]          = ticket.Title,
            ["new_description"]    = ticket.Description,
            // OptionSet values — pass int so DataverseService wraps in OptionSetValue
            ["new_status"]         = (int)ticket.Status,
            ["new_priority"]       = (int)ticket.Priority,
            ["new_category"]       = (int)ticket.Category,
            // FIX: lookup column uses LookupValue so SDK produces EntityReference
            ["new_customerid"]     = new LookupValue(CustomerEntityName, ticket.CustomerId),
            ["new_escalationcount"] = ticket.EscalationCount
        };

        if (ticket.AssignedToUserId is not null)
            attrs["new_assignedtouserid"] = ticket.AssignedToUserId;

        if (ticket.ResolvedAt.HasValue)
            attrs["new_resolvedat"] = ticket.ResolvedAt.Value.UtcDateTime;

        if (ticket.ClosedAt.HasValue)
            attrs["new_closedat"] = ticket.ClosedAt.Value.UtcDateTime;

        if (ticket.ResolutionNotes is not null)
            attrs["new_resolutionnotes"] = ticket.ResolutionNotes;

        return attrs;
    }

    // -------------------------------------------------------------------------
    // FetchXML helpers
    // -------------------------------------------------------------------------

    private static string AttributeColumns() =>
        string.Join(string.Empty, AllColumns.Select(c => $"<attribute name=\"{c}\" />"));

    private static List<string> BuildFilterConditions(TicketFilter filter)
    {
        var conditions = new List<string>();

        if (filter.Status.HasValue)
            // Integer values are safe to embed directly
            conditions.Add($"<condition attribute=\"new_status\" operator=\"eq\" value=\"{(int)filter.Status}\" />");

        if (filter.Priority.HasValue)
            conditions.Add($"<condition attribute=\"new_priority\" operator=\"eq\" value=\"{(int)filter.Priority}\" />");

        if (filter.Category.HasValue)
            conditions.Add($"<condition attribute=\"new_category\" operator=\"eq\" value=\"{(int)filter.Category}\" />");

        if (filter.CustomerId.HasValue)
            // Guid.ToString() produces a safe hex-dash string; no escaping needed
            conditions.Add($"<condition attribute=\"new_customerid\" operator=\"eq\" value=\"{filter.CustomerId}\" />");

        if (!string.IsNullOrWhiteSpace(filter.AssignedToUserId))
            // FIX: XML-escape user-supplied string
            conditions.Add(
                $"<condition attribute=\"new_assignedtouserid\" operator=\"eq\" " +
                $"value=\"{DataverseService.XmlEscape(filter.AssignedToUserId)}\" />");

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            // FIX: XML-escape search term — classic injection vector for LIKE queries
            conditions.Add(
                $"<condition attribute=\"new_title\" operator=\"like\" " +
                $"value=\"%{DataverseService.XmlEscape(filter.SearchTerm)}%\" />");

        return conditions;
    }

    private static string MapSortColumn(string sortBy) => sortBy.ToLowerInvariant() switch
    {
        "ticketnumber" => "new_ticketnumber",
        "title"        => "new_title",
        "status"       => "new_status",
        "priority"     => "new_priority",
        "updatedat"    => "modifiedon",
        _              => "createdon"
    };

    // -------------------------------------------------------------------------
    // Safe attribute extraction helpers
    // -------------------------------------------------------------------------

    private static Guid GetGuid(Dictionary<string, object> attrs, string key) =>
        attrs.TryGetValue(key, out var v) && v is Guid g ? g : Guid.Empty;

    private static string GetString(Dictionary<string, object> attrs, string key) =>
        attrs.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;

    private static string? GetStringOrNull(Dictionary<string, object> attrs, string key) =>
        attrs.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static int GetInt(Dictionary<string, object> attrs, string key)
    {
        if (attrs.TryGetValue(key, out var v))
        {
            if (v is int i) return i;
            if (int.TryParse(v?.ToString(), out var parsed)) return parsed;
        }
        return 0;
    }

    private static DateTimeOffset GetDateTimeOffset(Dictionary<string, object> attrs, string key) =>
        attrs.TryGetValue(key, out var v) && v is DateTime dt
            ? new DateTimeOffset(dt, TimeSpan.Zero)
            : DateTimeOffset.MinValue;

    private static DateTimeOffset? GetDateTimeOffsetOrNull(Dictionary<string, object> attrs, string key) =>
        attrs.TryGetValue(key, out var v) && v is DateTime dt
            ? new DateTimeOffset(dt, TimeSpan.Zero)
            : null;
}
