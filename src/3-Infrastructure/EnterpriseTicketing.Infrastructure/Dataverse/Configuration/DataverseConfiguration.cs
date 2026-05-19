using System.ComponentModel.DataAnnotations;

namespace EnterpriseTicketing.Infrastructure.Dataverse.Configuration;

/// <summary>
/// Strongly-typed configuration for Dataverse connectivity.
/// Bound from appsettings.json "Dataverse" section.
/// In production, ClientSecret is sourced from Azure Key Vault via Managed Identity —
/// never from appsettings.json directly.
///
/// Enterprise pattern: Use IOptions<T> + DataAnnotations validation at startup
/// to fail fast if configuration is missing, rather than failing at first request.
/// </summary>
public sealed class DataverseConfiguration
{
    public const string SectionName = "Dataverse";

    [Required]
    public string Url { get; set; } = string.Empty;

    [Required]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// In production, this is empty — sourced from Key Vault via Managed Identity.
    /// In development, sourced from user secrets or environment variables.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    public int MaxRetryCount { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Returns the Dataverse OData API base URL.</summary>
    public string ApiBaseUrl => $"{Url.TrimEnd('/')}/api/data/v9.2/";

    /// <summary>Returns the OAuth2 scope required for Dataverse access.</summary>
    public string Scope => $"{Url.TrimEnd('/')}/.default";
}
