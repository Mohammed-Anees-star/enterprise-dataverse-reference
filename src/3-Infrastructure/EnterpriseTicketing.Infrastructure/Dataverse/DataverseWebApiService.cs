using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnterpriseTicketing.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Infrastructure.Dataverse;

/// <summary>
/// Implements IDataverseWebApiService using HttpClient + Dataverse OData REST API.
///
/// When to use Web API vs SDK:
///   Web API is preferred when:
///   - The application runs outside Azure (Lambda, on-premises, non-Microsoft cloud)
///   - Language/framework interoperability is needed
///   - OData standard tooling is in use (e.g., Power Query, SSRS)
///   - Batch operations ($batch endpoint) are needed for atomic multi-record operations
///   - Change tracking via ETag/If-Match headers is required
///   - The SDK NuGet package size is a concern (e.g., Azure Functions with cold start)
///
/// The HttpClient is registered as a named client "DataverseWebApi" via IHttpClientFactory.
/// Token injection is handled by DataverseHttpClientHandler (DelegatingHandler).
/// Polly resilience policies are attached at registration time in DependencyInjection.
/// </summary>
public sealed class DataverseWebApiService : IDataverseWebApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DataverseWebApiService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public DataverseWebApiService(
        IHttpClientFactory httpClientFactory,
        ILogger<DataverseWebApiService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("DataverseWebApi");
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(
        string entitySetName,
        Guid id,
        string? select = null,
        string? expand = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var url = BuildEntityUrl(entitySetName, id, select, expand);

        _logger.LogDebug("Dataverse Web API GET {Url}", url);

        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound) return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    public async Task<ODataQueryResult<T>> QueryAsync<T>(
        string entitySetName,
        ODataQueryParameters parameters,
        CancellationToken cancellationToken = default) where T : class
    {
        var url = BuildQueryUrl(entitySetName, parameters);

        _logger.LogDebug("Dataverse Web API QUERY {Url}", url);

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var wrapper = await response.Content
            .ReadFromJsonAsync<ODataResponseWrapper<T>>(JsonOptions, cancellationToken);

        return new ODataQueryResult<T>
        {
            Value = wrapper?.Value ?? [],
            Count = wrapper?.OdataCount,
            NextLink = wrapper?.OdataNextLink
        };
    }

    public async Task<Guid> CreateAsync(
        string entitySetName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Dataverse Web API POST {EntitySet}", entitySetName);

        var response = await _httpClient.PostAsJsonAsync(entitySetName, payload, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Dataverse returns the new entity ID in the OData-EntityId header
        if (response.Headers.TryGetValues("OData-EntityId", out var values))
        {
            var entityIdUrl = values.FirstOrDefault();
            if (!string.IsNullOrEmpty(entityIdUrl))
            {
                // Extract Guid from URL format: .../entity(00000000-0000-...)
                var start = entityIdUrl.LastIndexOf('(') + 1;
                var end = entityIdUrl.LastIndexOf(')');
                if (start > 0 && end > start)
                    return Guid.Parse(entityIdUrl[start..end]);
            }
        }

        throw new InvalidOperationException("Dataverse did not return an entity ID in the response.");
    }

    public async Task UpdateAsync(
        string entitySetName,
        Guid id,
        object payload,
        string? etag = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{entitySetName}({id})";

        _logger.LogDebug("Dataverse Web API PATCH {Url}", url);

        using var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        // Optimistic concurrency: if etag provided, only update if record hasn't changed
        if (!string.IsNullOrEmpty(etag))
            request.Headers.Add("If-Match", etag);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(
        string entitySetName,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var url = $"{entitySetName}({id})";
        _logger.LogDebug("Dataverse Web API DELETE {Url}", url);

        var response = await _httpClient.DeleteAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<BatchResult>> ExecuteBatchAsync(
        IReadOnlyList<BatchRequest> requests,
        CancellationToken cancellationToken = default)
    {
        // OData $batch sends multiple operations in a single HTTP request
        // This is essential for performance when creating/updating multiple related records
        var batchId = $"batch_{Guid.NewGuid()}";
        var content = new MultipartContent("mixed", batchId);

        foreach (var req in requests)
        {
            var requestContent = new StringContent(req.Body is not null
                ? JsonSerializer.Serialize(req.Body, JsonOptions)
                : string.Empty);
            requestContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/http");
            requestContent.Headers.Add("Content-Transfer-Encoding", "binary");

            var httpMsg = $"{req.Method} {req.Url} HTTP/1.1\n" +
                          "Content-Type: application/json\n\n" +
                          (req.Body is not null ? JsonSerializer.Serialize(req.Body, JsonOptions) : string.Empty);

            content.Add(new StringContent(httpMsg));
        }

        var response = await _httpClient.PostAsync("$batch", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Parse batch response — simplified for reference implementation
        _logger.LogDebug("Dataverse batch completed with {Count} operations", requests.Count);

        return [new BatchResult { StatusCode = (int)response.StatusCode }];
    }

    private static string BuildEntityUrl(string entitySet, Guid id, string? select, string? expand)
    {
        var url = $"{entitySet}({id})";
        var queryParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(select))
            queryParts.Add($"$select={Uri.EscapeDataString(select)}");

        if (!string.IsNullOrWhiteSpace(expand))
            queryParts.Add($"$expand={Uri.EscapeDataString(expand)}");

        return queryParts.Count > 0 ? $"{url}?{string.Join("&", queryParts)}" : url;
    }

    private static string BuildQueryUrl(string entitySet, ODataQueryParameters parameters)
    {
        var queryParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(parameters.Select))
            queryParts.Add($"$select={parameters.Select}");

        if (!string.IsNullOrWhiteSpace(parameters.Filter))
            queryParts.Add($"$filter={parameters.Filter}");

        if (!string.IsNullOrWhiteSpace(parameters.OrderBy))
            queryParts.Add($"$orderby={parameters.OrderBy}");

        if (parameters.Top.HasValue)
            queryParts.Add($"$top={parameters.Top}");

        if (parameters.Skip.HasValue)
            queryParts.Add($"$skip={parameters.Skip}");

        if (!string.IsNullOrWhiteSpace(parameters.Expand))
            queryParts.Add($"$expand={parameters.Expand}");

        if (parameters.IncludeCount)
            queryParts.Add("$count=true");

        return queryParts.Count > 0 ? $"{entitySet}?{string.Join("&", queryParts)}" : entitySet;
    }

    private sealed class ODataResponseWrapper<T>
    {
        [JsonPropertyName("value")]
        public List<T> Value { get; init; } = [];

        [JsonPropertyName("@odata.count")]
        public int? OdataCount { get; init; }

        [JsonPropertyName("@odata.nextLink")]
        public string? OdataNextLink { get; init; }
    }
}
