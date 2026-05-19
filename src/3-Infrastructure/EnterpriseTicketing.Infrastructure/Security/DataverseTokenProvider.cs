using EnterpriseTicketing.Infrastructure.Dataverse.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;

namespace EnterpriseTicketing.Infrastructure.Security;

/// <summary>
/// Provides OAuth2 access tokens for Dataverse Web API calls.
/// Uses MSAL ConfidentialClientApplication with in-memory token caching.
///
/// Token caching strategy:
///   MSAL handles its own in-memory cache internally. We add an additional
///   outer cache with a 5-minute buffer before expiry to prevent requesting
///   tokens on every API call.
///
/// Production upgrade path:
///   Replace client secret with Managed Identity:
///   var credential = new DefaultAzureCredential();
///   var token = await credential.GetTokenAsync(new TokenRequestContext([scope]));
/// </summary>
public sealed class DataverseTokenProvider : IDataverseTokenProvider
{
    private readonly IConfidentialClientApplication _msalClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DataverseTokenProvider> _logger;
    private readonly string _scope;
    private const string CacheKey = "DataverseAccessToken";
    private static readonly TimeSpan TokenExpiryBuffer = TimeSpan.FromMinutes(5);

    public DataverseTokenProvider(
        IOptions<DataverseConfiguration> configuration,
        IMemoryCache cache,
        ILogger<DataverseTokenProvider> logger)
    {
        var config = configuration.Value;
        _cache = cache;
        _logger = logger;
        _scope = config.Scope;

        // Build MSAL confidential client — uses client credentials flow
        _msalClient = ConfidentialClientApplicationBuilder
            .Create(config.ClientId)
            .WithClientSecret(config.ClientSecret)
            .WithAuthority(AzureCloudInstance.AzurePublic, config.TenantId)
            .Build();
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        // Check outer cache first (avoids MSAL call overhead on every HTTP request)
        if (_cache.TryGetValue<string>(CacheKey, out var cachedToken) && !string.IsNullOrEmpty(cachedToken))
            return cachedToken;

        _logger.LogDebug("Acquiring new Dataverse access token");

        var result = await _msalClient
            .AcquireTokenForClient([_scope])
            .ExecuteAsync(cancellationToken);

        // Cache with buffer: expire our cache 5 minutes before the actual token expiry
        var cacheExpiry = result.ExpiresOn - DateTimeOffset.UtcNow - TokenExpiryBuffer;
        if (cacheExpiry > TimeSpan.Zero)
        {
            _cache.Set(CacheKey, result.AccessToken, cacheExpiry);
        }

        _logger.LogDebug("Acquired Dataverse access token, expires at {ExpiresOn}", result.ExpiresOn);
        return result.AccessToken;
    }
}

public interface IDataverseTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
