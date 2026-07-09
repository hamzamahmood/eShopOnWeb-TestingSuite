using System;

namespace Microsoft.eShopWeb.Infrastructure.Configuration;

// Typed options for the Maxio integration (mirrors CatalogSettings usage). Bound
// from the "Maxio" configuration section; the API key arrives via user-secrets
// and is never committed. See plan §5 for the full key list.
public class MaxioSettings
{
    // Credentials & tenant
    public string? ApiKey { get; set; }
    public string? Subdomain { get; set; }

    // Maxio DATA-CENTER REGION (US/EU) — NOT the deployment target (plan §2.3).
    public string Environment { get; set; } = "US";

    // Optional explicit outbound base URL. When set it WINS verbatim over the
    // subdomain-derived host, so the SAME build can target production, a
    // dev/sandbox tenant, or a local mock server purely through configuration
    // (plan §2.3 / §4.3). Leave empty to derive the host from Subdomain (+ region).
    public string? BaseUrl { get; set; }

    // Seeded demo entities (plan §5). Ids are assigned by Maxio and are not
    // secret; handles are stable.
    public string ProductFamilyHandle { get; set; } = "eshop-subscribe";
    public int ProductFamilyId { get; set; }

    public string DefaultProductHandle { get; set; } = "eshop-pro";
    public int DefaultProductId { get; set; }

    public string AlternateProductHandle { get; set; } = "basic-plan";
    public int AlternateProductId { get; set; }

    public string MeteredComponentHandle { get; set; } = "api-call";
    public int MeteredComponentId { get; set; }

    public bool IsEuRegion => string.Equals(Environment, "EU", StringComparison.OrdinalIgnoreCase);

    // Resolution order the client MUST honor (plan §2.3): an explicit BaseUrl is
    // used verbatim; otherwise the host is derived from Subdomain (+ region).
    public string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            return BaseUrl.TrimEnd('/');
        }

        var site = string.IsNullOrWhiteSpace(Subdomain) ? "subdomain" : Subdomain.Trim();
        return IsEuRegion
            ? $"https://{site}.ebilling.maxio.com"
            : $"https://{site}.chargify.com";
    }
}
