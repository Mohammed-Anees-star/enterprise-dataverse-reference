namespace EnterpriseTicketing.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the Dataverse SDK.
/// The Application layer declares what it needs; Infrastructure supplies the
/// <c>ServiceClient</c>-backed implementation.
///
/// Attribute value conventions (see <c>DataverseService.cs</c> for details):
///   - Lookup Guids must be wrapped in <see cref="LookupValue"/> by the caller.
///   - Primary-key Guids are passed as raw <see cref="Guid"/>.
///   - OptionSet / enum values are passed as <see cref="int"/>.
///   - All other types are passed as-is.
/// </summary>
public interface IDataverseService
{
    /// <summary>Creates a record and returns its new GUID.</summary>
    Task<Guid> CreateEntityAsync(
        string entityLogicalName,
        Dictionary<string, object> attributes,
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves a single record by GUID. Returns <c>null</c> if not found.</summary>
    Task<Dictionary<string, object>?> GetEntityAsync(
        string entityLogicalName,
        Guid id,
        IEnumerable<string> columns,
        CancellationToken cancellationToken = default);

    /// <summary>Updates an existing record.</summary>
    Task UpdateEntityAsync(
        string entityLogicalName,
        Guid id,
        Dictionary<string, object> attributes,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a record.</summary>
    Task DeleteEntityAsync(
        string entityLogicalName,
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a FetchXML query.
    /// Caller is responsible for XML-escaping any string values interpolated into the XML.
    /// </summary>
    Task<(IReadOnlyList<Dictionary<string, object>> Records, string? NextPageCookie)> QueryEntitiesAsync(
        string entityLogicalName,
        string fetchXml,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Like <see cref="QueryEntitiesAsync"/> but also returns the server-computed total
    /// record count (requires <c>returntotalrecordcount="true"</c> in the FetchXML).
    /// Use this for paginated list endpoints.
    /// </summary>
    Task<(IReadOnlyList<Dictionary<string, object>> Records, int TotalRecordCount, string? NextPageCookie)>
        QueryEntitiesWithCountAsync(
            string entityLogicalName,
            string fetchXml,
            CancellationToken cancellationToken = default);

    /// <summary>Returns <c>true</c> if a record with the given GUID exists.</summary>
    Task<bool> ExistsAsync(
        string entityLogicalName,
        Guid id,
        CancellationToken cancellationToken = default);
}
