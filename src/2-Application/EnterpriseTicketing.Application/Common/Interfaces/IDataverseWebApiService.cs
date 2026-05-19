namespace EnterpriseTicketing.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the Dataverse Web API (OData REST endpoint).
/// Used as an alternative to IDataverseService when HTTP-native patterns are preferred:
///   - Cross-language microservice integration
///   - Standard OData tooling compatibility
///   - When running outside the Dataverse plugin sandbox
///   - Batch operations via OData $batch
/// See: ADR-001 in docs/architecture/19-tradeoff-analysis.md
/// </summary>
public interface IDataverseWebApiService
{
    Task<T?> GetAsync<T>(
        string entitySetName,
        Guid id,
        string? select = null,
        string? expand = null,
        CancellationToken cancellationToken = default) where T : class;

    Task<ODataQueryResult<T>> QueryAsync<T>(
        string entitySetName,
        ODataQueryParameters parameters,
        CancellationToken cancellationToken = default) where T : class;

    Task<Guid> CreateAsync(
        string entitySetName,
        object payload,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        string entitySetName,
        Guid id,
        object payload,
        string? etag = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string entitySetName,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BatchResult>> ExecuteBatchAsync(
        IReadOnlyList<BatchRequest> requests,
        CancellationToken cancellationToken = default);
}

public sealed record ODataQueryParameters
{
    public string? Select { get; init; }
    public string? Filter { get; init; }
    public string? OrderBy { get; init; }
    public int? Top { get; init; }
    public int? Skip { get; init; }
    public string? Expand { get; init; }
    public bool IncludeCount { get; init; }
}

public sealed record ODataQueryResult<T>
{
    public required IReadOnlyList<T> Value { get; init; }
    public int? Count { get; init; }
    public string? NextLink { get; init; }
}

public sealed record BatchRequest
{
    public required string Method { get; init; }
    public required string Url { get; init; }
    public object? Body { get; init; }
}

public sealed record BatchResult
{
    public required int StatusCode { get; init; }
    public string? Body { get; init; }
}
