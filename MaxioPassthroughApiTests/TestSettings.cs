namespace MaxioPassthroughApiTests;

/// <summary>
/// External configuration for the black-box suite. Everything is overridable by environment variable so the
/// SAME tests can run against either integration (eShopOnWeb-Direct or eShopOnWeb-Plugin) — just point
/// <c>PUBLICAPI_BASEURL</c> at whichever PublicApi is running. Defaults match the Maxio mock server's canned
/// data (openAPI/MaxioMockServer/MockData).
/// </summary>
public static class TestSettings
{
    /// <summary>Base URL of the running eShopOnWeb PublicApi under test.</summary>
    public static string BaseUrl =>
        (Environment.GetEnvironmentVariable("PUBLICAPI_BASEURL") ?? "https://localhost:5099").TrimEnd('/');

    /// <summary>A customer reference the mock knows (→ customer id 98765).</summary>
    public static string KnownCustomerReference => Get("KNOWN_CUSTOMER_REFERENCE", "cust_12345");

    /// <summary>The Maxio customer id the mock has subscriptions for.</summary>
    public static string KnownCustomerId => Get("KNOWN_CUSTOMER_ID", "98765");

    /// <summary>A reference the mock does NOT know (drives Maxio's 404 path).</summary>
    public static string UnknownCustomerReference => Get("UNKNOWN_CUSTOMER_REFERENCE", "no_such_customer_ref");

    /// <summary>A well-formed but unknown numeric customer id (numeric so both integrations behave identically).</summary>
    public static string UnknownCustomerId => Get("UNKNOWN_CUSTOMER_ID", "99999999");

    private static string Get(string key, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
