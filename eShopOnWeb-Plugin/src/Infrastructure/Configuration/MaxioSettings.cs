namespace Microsoft.eShopWeb.Infrastructure.Configuration;

/// <summary>
/// Typed configuration for the Maxio Advanced Billing integration. Bound from the "Maxio" configuration
/// section (user-secrets in development; never <c>appsettings.json</c> - see integration plan §6).
/// </summary>
public class MaxioSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;

    /// <summary>"US" (default) or "EU".</summary>
    public string Environment { get; set; } = "US";

    public string BaseUrl { get; set; } = string.Empty;

    public string ProductFamilyHandle { get; set; } = string.Empty;
    public long ProductFamilyId { get; set; }

    public string DefaultProductHandle { get; set; } = string.Empty;
    public long DefaultProductId { get; set; }

    public string AlternateProductHandle { get; set; } = string.Empty;
    public long AlternateProductId { get; set; }

    public string MeteredComponentHandle { get; set; } = string.Empty;
    public long MeteredComponentId { get; set; }
}
