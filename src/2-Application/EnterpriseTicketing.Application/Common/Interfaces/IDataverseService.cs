namespace EnterpriseTicketing.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the Dataverse SDK (Microsoft.PowerPlatform.Dataverse.Client.ServiceClient).
/// The Application layer declares what it needs; Infrastructure provides the ServiceClient implementation.
///
/// Design note: This interface is intentionally generic (Dictionary-based) rather than
/// entity-specific. Repositories translate between domain entities and these primitives.
/// Strongly-typed Dataverse entity classes are kept in Infrastructure only.
/// </summary>
public interface IDataverseService
{
    /// <summary>Creates a record and returns its new ID.</summary>
    Task<Guid> CreateEntityAsync(
        string entityLogicalName,
        Dictionary<string, object> attributes,
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves a single record by ID. Returns null if not found.</summary>
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

    /// <summary>Executes a FetchXML query and returns matching records.</summary>
    Task<(IReadOnlyList<Dictionary<string, object>> Records, string? NextPageCookie)> QueryEntitiesAsync(
        string entityLogicalName,
        string fetchXml,
        CancellationToken cancellationToken = default);

    /// <summary>Checks whether a record exists.</summary>
    Task<bool> ExistsAsync(
        string entityLogicalName,
        Guid id,
        CancellationToken cancellationToken = default);
}
