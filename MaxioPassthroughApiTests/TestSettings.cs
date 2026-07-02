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

    /// <summary>
    /// Path of the <c>MaxioBillingController</c> list-plans endpoint — identical route on both integrations
    /// (<c>/api/maxio/product-families/{productFamilyId}/products</c>). The id is inert on both — each
    /// client always uses its own server-configured product family — so any value works; the mock's known
    /// id 527890 is used here. Kept overridable via <c>LIST_PLANS_PATH</c> in case a future deployment's
    /// route differs.
    /// </summary>
    public static string ListPlansPath =>
        Get("LIST_PLANS_PATH", "/api/maxio/product-families/527890/products");

    /// <summary>Builds the list-customer-subscriptions path (same route on both integrations).</summary>
    public static string CustomerSubscriptionsPath(string customerId) =>
        $"/api/maxio/customers/{customerId}/subscriptions";

    /// <summary>Path of the find-or-create-customer endpoint (identical route on both integrations).</summary>
    public static string CustomersPath => "/api/maxio/customers";

    /// <summary>Path of the create-subscription endpoint (identical route on both integrations).</summary>
    public static string SubscriptionsPath => "/api/maxio/subscriptions";

    /// <summary>Builds the read/pause/resume/reactivate/migrate/cancel subscription path (identical route shape on both integrations).</summary>
    public static string SubscriptionPath(string subscriptionId) => $"/api/maxio/subscriptions/{subscriptionId}";

    public static string PauseSubscriptionPath(string subscriptionId) => $"{SubscriptionPath(subscriptionId)}/hold";

    public static string ResumeSubscriptionPath(string subscriptionId) => $"{SubscriptionPath(subscriptionId)}/resume";

    public static string ReactivateSubscriptionPath(string subscriptionId) => $"{SubscriptionPath(subscriptionId)}/reactivate";

    public static string MigrationsPath(string subscriptionId) => $"{SubscriptionPath(subscriptionId)}/migrations";

    /// <summary>
    /// Builds the record-usage path. Routes genuinely differ between integrations (Direct has no
    /// component-id segment; Plugin's is present but inert), so — like <see cref="ListPlansPath"/> — this is
    /// configurable via <c>RECORD_USAGE_PATH_TEMPLATE</c> (must contain a literal <c>{subscriptionId}</c>
    /// placeholder). Defaults to the Plugin form; set
    /// <c>RECORD_USAGE_PATH_TEMPLATE=/api/maxio/subscriptions/{{subscriptionId}}/usages</c> for Direct.
    /// </summary>
    public static string RecordUsagePath(string subscriptionId) =>
        Get("RECORD_USAGE_PATH_TEMPLATE", "/api/maxio/subscriptions/{subscriptionId}/components/1/usages")
            .Replace("{subscriptionId}", subscriptionId);

    /// <summary>A customer reference the mock knows (→ customer id 98765).</summary>
    public static string KnownCustomerReference => Get("KNOWN_CUSTOMER_REFERENCE", "cust_12345");

    /// <summary>The Maxio customer id the mock has subscriptions for.</summary>
    public static string KnownCustomerId => Get("KNOWN_CUSTOMER_ID", "98765");

    /// <summary>A reference the mock does NOT know (drives Maxio's 404 path).</summary>
    public static string UnknownCustomerReference => Get("UNKNOWN_CUSTOMER_REFERENCE", "no_such_customer_ref");

    /// <summary>A well-formed but unknown numeric customer id (numeric so both integrations behave identically).</summary>
    public static string UnknownCustomerId => Get("UNKNOWN_CUSTOMER_ID", "99999999");

    /// <summary>Builds a fresh, never-before-seen customer reference for a find-or-create-customer test run.</summary>
    public static string NewCustomerReference() => $"newcust_{Guid.NewGuid():N}";

    /// <summary>The mock's canned active subscription (state "active", product "gold", customer 98765).</summary>
    public static string KnownActiveSubscriptionId => Get("KNOWN_ACTIVE_SUBSCRIPTION_ID", "15100121");

    /// <summary>The mock's canned on-hold subscription (state "on_hold").</summary>
    public static string KnownOnHoldSubscriptionId => Get("KNOWN_ON_HOLD_SUBSCRIPTION_ID", "15100210");

    /// <summary>The mock's canned canceled subscription (state "canceled").</summary>
    public static string KnownCanceledSubscriptionId => Get("KNOWN_CANCELED_SUBSCRIPTION_ID", "15100299");

    /// <summary>A well-formed but unknown numeric subscription id (distinct from <see cref="UnknownCustomerId"/> for clarity).</summary>
    public static string UnknownSubscriptionId => Get("UNKNOWN_SUBSCRIPTION_ID", "88888888");

    /// <summary>The active subscription's current product handle (see <see cref="KnownActiveSubscriptionId"/>).</summary>
    public static string KnownProductHandle => Get("KNOWN_PRODUCT_HANDLE", "gold");

    /// <summary>A second known product handle, distinct from <see cref="KnownProductHandle"/> — used as a migration target.</summary>
    public static string AlternateProductHandle => Get("ALTERNATE_PRODUCT_HANDLE", "zero-dollar-product");

    /// <summary>A well-formed but unknown product handle (drives Maxio's "Invalid Product" / "Product doesn't exist" validation path).</summary>
    public static string UnknownProductHandle => Get("UNKNOWN_PRODUCT_HANDLE", "no-such-plan");

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
