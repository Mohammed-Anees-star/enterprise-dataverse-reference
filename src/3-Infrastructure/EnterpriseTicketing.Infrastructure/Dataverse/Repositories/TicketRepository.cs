using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Domain.Entities;
using EnterpriseTicketing.Domain.Enums;
using EnterpriseTicketing.Domain.Interfaces;
using EnterpriseTicketing.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Infrastructure.Dataverse.Repositories;

/// <summary>
/// Dataverse-backed implementation of ITicketRepository.
/// Maps between the domain's Ticket entity and Dataverse attribute dictionaries.
///
/// Dataverse table: new_ticket
/// This "new_" prefix is the default publisher prefix for custom tables.
/// In production, replace with your organization's publisher prefix (e.g., "contoso_ticket").
///
/// Architecture note: The repository is the boundary between the domain model and
/// the Dataverse data model. All FetchXML, attribute names, and OptionSet value mappings
/// live here — the domain model is completely unaware of Dataverse specifics.
/// </summary>
public sealed class TicketRepository : ITicketRepository
{
    private const string EntityName = "new_ticket";
    private const string EntityIdAttribute = "new_ticketid";

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
        var attributes = await _dataverseService.GetEntityAsync(EntityName, id, AllColumns, cancellationToken);
        return attributes is not null ? MapToTicket(attributes) : null;
    }

    public async Task<Ticket?> GetByTicketNumberAsync(string ticketNumber, CancellationToken cancellationToken = default)
    {
        var fetchXml = $"""
            <fetch top="1">
              <entity name="{EntityName}">
                {string.Join("\n", AllColumns.Select(c => $"<attribute name=\"{c}\" />"))}
                <filter>
                  <condition attribute="new_ticketnumber" operator="eq" value="{ticketNumber}" />
                </filter>
              </entity>
            </fetch>
            """;

        var (records, _) = await _dataverseService.QueryEntitiesAsync(EntityName, fetchXml, cancellationToken);
        return records.FirstOrDefault() is { } attrs ? MapToTicket(attrs) : null;
    }

    public async Task<(IReadOnlyList<Ticket> Items, int TotalCount)> GetPagedAsync(
        TicketFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var conditions = BuildFilterConditions(filter);
        var orderBy = MapSortColumn(filter.SortBy);
        var orderDirection = filter.SortDescending ? "descending" : "ascending";
        var pageOffset = (pageNumber - 1) * pageSize;

        var fetchXml = $"""
            <fetch count="{pageSize}" page="{pageNumber}" returntotalrecordcount="true">
              <entity name="{EntityName}">
                {string.Join("\n", AllColumns.Select(c => $"<attribute name=\"{c}\" />"))}
                {(conditions.Length > 0 ? $"<filter type=\"and\">{conditions}</filter>" : string.Empty)}
                <order attribute="{orderBy}" descending="{filter.SortDescending.ToString().ToLower()}" />
              </entity>
            </fetch>
            """;

        var (records, _) = await _dataverseService.QueryEntitiesAsync(EntityName, fetchXml, cancellationToken);

        var tickets = records.Select(MapToTicket).ToList();

        // Note: FetchXML with returntotalrecordcount=true returns count in paging cookie
        // For simplicity in this reference implementation, we return actual records count
        // In production, parse the paging cookie for accurate total count
        var totalCount = tickets.Count < pageSize ? pageOffset + tickets.Count : pageOffset + tickets.Count + 1;

        return (tickets.AsReadOnly(), totalCount);
    }

    public async Task<IReadOnlyList<Ticket>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var fetchXml = $"""
            <fetch>
              <entity name="{EntityName}">
                {string.Join("\n", AllColumns.Select(c => $"<attribute name=\"{c}\" />"))}
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
        var attributes = MapToAttributes(ticket);
        await _dataverseService.CreateEntityAsync(EntityName, attributes, cancellationToken);
        _logger.LogDebug("Persisted new ticket {TicketNumber} ({TicketId})", ticket.TicketNumber, ticket.Id);
    }

    public async Task UpdateAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        var attributes = MapToAttributes(ticket);
        await _dataverseService.UpdateEntityAsync(EntityName, ticket.Id, attributes, cancellationToken);
        _logger.LogDebug("Updated ticket {TicketId}", ticket.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _dataverseService.DeleteEntityAsync(EntityName, id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dataverseService.ExistsAsync(EntityName, id, cancellationToken);
    }

    private static Ticket MapToTicket(Dictionary<string, object> attrs)
    {
        return Ticket.Reconstitute(
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
    }

    private static Dictionary<string, object> MapToAttributes(Ticket ticket)
    {
        var attrs = new Dictionary<string, object>
        {
            ["new_ticketid"] = ticket.Id,
            ["new_ticketnumber"] = ticket.TicketNumber.Value,
            ["new_title"] = ticket.Title,
            ["new_description"] = ticket.Description,
            ["new_status"] = (int)ticket.Status,
            ["new_priority"] = (int)ticket.Priority,
            ["new_category"] = (int)ticket.Category,
            ["new_customerid"] = ticket.CustomerId,
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

    private static string BuildFilterConditions(TicketFilter filter)
    {
        var conditions = new List<string>();

        if (filter.Status.HasValue)
            conditions.Add($"<condition attribute=\"new_status\" operator=\"eq\" value=\"{(int)filter.Status}\" />");

        if (filter.Priority.HasValue)
            conditions.Add($"<condition attribute=\"new_priority\" operator=\"eq\" value=\"{(int)filter.Priority}\" />");

        if (filter.Category.HasValue)
            conditions.Add($"<condition attribute=\"new_category\" operator=\"eq\" value=\"{(int)filter.Category}\" />");

        if (filter.CustomerId.HasValue)
            conditions.Add($"<condition attribute=\"new_customerid\" operator=\"eq\" value=\"{filter.CustomerId}\" />");

        if (!string.IsNullOrWhiteSpace(filter.AssignedToUserId))
            conditions.Add($"<condition attribute=\"new_assignedtouserid\" operator=\"eq\" value=\"{filter.AssignedToUserId}\" />");

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            conditions.Add($"<condition attribute=\"new_title\" operator=\"like\" value=\"%{filter.SearchTerm}%\" />");

        return string.Join("\n", conditions);
    }

    private static string MapSortColumn(string sortBy) => sortBy.ToLowerInvariant() switch
    {
        "ticketnumber" => "new_ticketnumber",
        "title" => "new_title",
        "status" => "new_status",
        "priority" => "new_priority",
        "updatedat" => "modifiedon",
        _ => "createdon"
    };

    // Attribute extraction helpers — defensive coding handles missing/null attributes gracefully
    private static Guid GetGuid(Dictionary<string, object> attrs, string key)
        => attrs.TryGetValue(key, out var v) && v is Guid g ? g : Guid.Empty;

    private static string GetString(Dictionary<string, object> attrs, string key)
        => attrs.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;

    private static string? GetStringOrNull(Dictionary<string, object> attrs, string key)
        => attrs.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static int GetInt(Dictionary<string, object> attrs, string key)
        => attrs.TryGetValue(key, out var v) && v is int i ? i :
           attrs.TryGetValue(key, out v) && int.TryParse(v?.ToString(), out var parsed) ? parsed : 0;

    private static DateTimeOffset GetDateTimeOffset(Dictionary<string, object> attrs, string key)
        => attrs.TryGetValue(key, out var v) && v is DateTime dt ? new DateTimeOffset(dt, TimeSpan.Zero) : DateTimeOffset.MinValue;

    private static DateTimeOffset? GetDateTimeOffsetOrNull(Dictionary<string, object> attrs, string key)
        => attrs.TryGetValue(key, out var v) && v is DateTime dt ? new DateTimeOffset(dt, TimeSpan.Zero) : null;
}
