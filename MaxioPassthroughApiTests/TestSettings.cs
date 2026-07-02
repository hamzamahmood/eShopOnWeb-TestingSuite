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

    /// <summary>
    /// Prefix for references whose FIRST mock request fails with a transient <c>503</c> and whose retried
    /// request succeeds. See <see cref="NewTransient5xxReference"/>. Used by the differentiator suite: the
    /// Plugin's SDK retries the idempotent GET and recovers (→ 200); the Direct passthrough has no
    /// resilience pipeline and surfaces the 503.
    /// </summary>
    public static string Transient5xxReferencePrefix => Get("TRANSIENT_5XX_REFERENCE_PREFIX", "retry_");

    /// <summary>
    /// Prefix for references whose FIRST mock request fails with a <c>429 Too Many Requests</c> (Maxio's
    /// documented rate-limit response) and whose retried request succeeds. See
    /// <see cref="NewRateLimitReference"/>. Same retry mechanism as <see cref="Transient5xxReferencePrefix"/>.
    /// </summary>
    public static string RateLimitReferencePrefix => Get("RATE_LIMIT_REFERENCE_PREFIX", "ratelimit_");

    /// <summary>Builds a fresh transient-<c>503</c> reference with a unique nonce for a single test run.</summary>
    public static string NewTransient5xxReference() => $"{Transient5xxReferencePrefix}{Guid.NewGuid():N}";

    /// <summary>Builds a fresh <c>429</c> rate-limit reference with a unique nonce for a single test run.</summary>
    public static string NewRateLimitReference() => $"{RateLimitReferencePrefix}{Guid.NewGuid():N}";

    private static string Get(string key, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
