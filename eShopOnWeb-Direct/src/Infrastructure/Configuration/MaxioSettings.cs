using System;

namespace Microsoft.eShopWeb.Infrastructure.Configuration;

/// <summary>
/// Typed options for the Maxio Advanced Billing integration (bound from the "Maxio" configuration
/// section, mirroring how <c>CatalogSettings</c> is bound). Only <see cref="ApiKey"/> is a secret and
/// must come from .NET user-secrets / environment variables — never source or appsettings.json.
/// The handles/IDs and <see cref="BaseUrl"/> are environment metadata, not secrets.
/// </summary>
public class MaxioSettings
{
    /// <summary>Maxio API key. Secret — supply via user-secrets or the <c>Maxio__ApiKey</c> environment variable.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The Maxio site subdomain, e.g. <c>apimatic-hackathon</c>.</summary>
    public string Subdomain { get; set; } = string.Empty;

    /// <summary>Maxio data-center region (US or EU). This is NOT the deployment target (see <see cref="BaseUrl"/>).</summary>
    public string Environment { get; set; } = "US";

    /// <summary>
    /// Optional explicit outbound base URL. When set it WINS over the subdomain-derived host, so the
    /// identical build can be pointed at production, a dev/sandbox tenant, or a local mock server purely
    /// through configuration — never a code change (plan §2.3). Leave empty to derive from
    /// <see cref="Subdomain"/> (+ <see cref="Environment"/>).
    /// </summary>
    public string? BaseUrl { get; set; }

    public string ProductFamilyHandle { get; set; } = string.Empty;
    public int ProductFamilyId { get; set; }

    public string DefaultProductHandle { get; set; } = string.Empty;
    public int DefaultProductId { get; set; }

    public string AlternateProductHandle { get; set; } = string.Empty;
    public int AlternateProductId { get; set; }

    public string MeteredComponentHandle { get; set; } = string.Empty;
    public int MeteredComponentId { get; set; }

    /// <summary>
    /// Resolves the outbound base URL: the explicit <see cref="BaseUrl"/> override when present,
    /// otherwise the host derived from <see cref="Subdomain"/> and the data-center <see cref="Environment"/>.
    /// This is the single place retargeting (prod / dev / mock) happens (plan §2.3 / §4.3).
    /// </summary>
    public string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            return BaseUrl.TrimEnd('/');
        }

        if (string.IsNullOrWhiteSpace(Subdomain))
        {
            throw new InvalidOperationException(
                "Maxio configuration is incomplete: set either 'Maxio:BaseUrl' or 'Maxio:Subdomain'.");
        }

        // Region axis (US/EU) per the Maxio OpenAPI x-server-configuration.
        return Environment.Trim().ToUpperInvariant() == "EU"
            ? $"https://{Subdomain}.ebilling.maxio.com"
            : $"https://{Subdomain}.chargify.com";
    }
}
