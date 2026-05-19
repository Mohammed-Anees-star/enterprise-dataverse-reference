using EnterpriseTicketing.Infrastructure.Security;

namespace EnterpriseTicketing.Infrastructure.Http;

/// <summary>
/// DelegatingHandler that injects the Dataverse OAuth2 bearer token into every HTTP request.
/// Registered as a typed DelegatingHandler in the named HttpClient pipeline.
///
/// Using a DelegatingHandler for token injection is the enterprise pattern:
///   - Token logic is in one place (no duplicate code in service methods)
///   - Automatically handles token refresh (DataverseTokenProvider caches intelligently)
///   - Testable by substituting the handler in unit tests
///   - Works with IHttpClientFactory's message handler pipeline
///
/// Also sets required Dataverse OData headers on every request.
/// </summary>
public sealed class DataverseHttpClientHandler : DelegatingHandler
{
    private readonly IDataverseTokenProvider _tokenProvider;

    public DataverseHttpClientHandler(IDataverseTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Required Dataverse OData headers
        if (!request.Headers.Contains("OData-MaxVersion"))
            request.Headers.Add("OData-MaxVersion", "4.0");

        if (!request.Headers.Contains("OData-Version"))
            request.Headers.Add("OData-Version", "4.0");

        if (!request.Headers.Contains("Prefer"))
            request.Headers.Add("Prefer", "odata.include-annotations=\"*\"");

        return await base.SendAsync(request, cancellationToken);
    }
}
