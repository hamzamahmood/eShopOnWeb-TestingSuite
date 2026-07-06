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

    /// <summary>
    /// Builds the read-only customer-lookup path. This endpoint exists ONLY on the Plugin integration
    /// (<c>GET /api/maxio/customers/lookup?reference=…</c>, <c>MaxioBillingController.FindCustomerId</c>);
    /// the Direct integration never built a standalone lookup route, so a known-reference request 404s there.
    /// </summary>
    public static string CustomerLookupPath(string reference) =>
        $"/api/maxio/customers/lookup?reference={Uri.EscapeDataString(reference)}";

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

    /// <summary>
    /// A well-formed but unknown numeric customer id (numeric so both integrations behave identically). The
    /// mock answers with a 404, but list-customer-subscriptions has no typed not-found exception on either
    /// integration, so the controller remaps it to 422 before it reaches the caller.
    /// </summary>
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

    /// <summary>
    /// A canned subscription whose provider <c>state</c> is a plausible-but-unknown value (<c>"assessing"</c>)
    /// that is in neither integration's known-state list. The Plugin's <c>MapState</c> maps it to the safe
    /// default <c>Other</c>; the Direct client forwards the raw string. Read-only (see the mock's
    /// <c>SubscriptionsById</c> note).
    /// </summary>
    public static string UnknownStateSubscriptionId => Get("UNKNOWN_STATE_SUBSCRIPTION_ID", "15100377");

    /// <summary>The active subscription's current product handle (see <see cref="KnownActiveSubscriptionId"/>).</summary>
    public static string KnownProductHandle => Get("KNOWN_PRODUCT_HANDLE", "gold");

    /// <summary>A second known product handle, distinct from <see cref="KnownProductHandle"/> — used as a migration target.</summary>
    public static string AlternateProductHandle => Get("ALTERNATE_PRODUCT_HANDLE", "zero-dollar-product");

    /// <summary>A well-formed but unknown product handle (drives Maxio's "Invalid Product" / "Product doesn't exist" validation path).</summary>
    public static string UnknownProductHandle => Get("UNKNOWN_PRODUCT_HANDLE", "no-such-plan");

    /// <summary>
    /// Prefix for references whose FIRST mock request fails with a transient <c>503</c> and whose retried
    /// request succeeds. See <see cref="NewTransient5xxReference"/>. NOTE: this is not a Plugin-vs-Direct
    /// differentiator — both integrations retry idempotent GETs (Direct via a
    /// <c>Microsoft.Extensions.Http.Resilience</c>/Polly pipeline, Plugin via the SDK's default
    /// <c>RetryOptions</c>), so both recover from the transient failure on the customer-lookup GET. No test
    /// currently exercises this prefix.
    /// </summary>
    public static string Transient5xxReferencePrefix => Get("TRANSIENT_5XX_REFERENCE_PREFIX", "retry_");

    /// <summary>
    /// Prefix for references whose FIRST mock request fails with a <c>429 Too Many Requests</c> (Maxio's
    /// documented rate-limit response) and whose retried request succeeds. See
    /// <see cref="NewRateLimitReference"/>. Same retry mechanism as <see cref="Transient5xxReferencePrefix"/>
    /// — and, like it, not a Plugin-vs-Direct differentiator (both integrations retry the idempotent GET).
    /// </summary>
    public static string RateLimitReferencePrefix => Get("RATE_LIMIT_REFERENCE_PREFIX", "ratelimit_");

    /// <summary>Builds a fresh transient-<c>503</c> reference with a unique nonce for a single test run.</summary>
    public static string NewTransient5xxReference() => $"{Transient5xxReferencePrefix}{Guid.NewGuid():N}";

    /// <summary>Builds a fresh <c>429</c> rate-limit reference with a unique nonce for a single test run.</summary>
    public static string NewRateLimitReference() => $"{RateLimitReferencePrefix}{Guid.NewGuid():N}";

    /// <summary>
    /// Prefix for references whose FIRST mock lookup is interrupted by a simulated transport-level connection
    /// break (the mock resets the connection) and whose retried lookup succeeds. Exercises the client's
    /// retry-on-transport-error recovery end-to-end. Not a Plugin-vs-Direct differentiator (both retry the
    /// idempotent GET). See <see cref="NewConnectionInterruptReference"/>.
    /// </summary>
    public static string ConnectionInterruptReferencePrefix => Get("CONNECTION_INTERRUPT_REFERENCE_PREFIX", "connbreak_");

    /// <summary>Builds a fresh connection-interruption reference with a unique nonce for a single test run.</summary>
    public static string NewConnectionInterruptReference() => $"{ConnectionInterruptReferencePrefix}{Guid.NewGuid():N}";

    /// <summary>
    /// Prefix for references that reproduce a find-or-create concurrent-create race: the mock's FIRST lookup
    /// misses (404) so the caller proceeds to create, the create then loses to a concurrent create (422
    /// "Reference has already been taken"), and a re-lookup now finds the customer (200). Used by the
    /// Plugin-advantage suite — the Plugin recovers by re-reading, the Direct client does not. See
    /// <see cref="NewRaceReference"/>.
    /// </summary>
    public static string RaceReferencePrefix => Get("RACE_REFERENCE_PREFIX", "race_");

    /// <summary>Builds a fresh concurrent-create-race reference with a unique nonce for a single test run.</summary>
    public static string NewRaceReference() => $"{RaceReferencePrefix}{Guid.NewGuid():N}";

    /// <summary>
    /// A product handle the mock treats as requiring a verified payment method the caller did not supply, so
    /// <c>createSubscription</c> returns a <c>422</c> with card/payment validation messages. The Plugin
    /// classifies these as a typed <c>PaymentVerificationRequiredException</c>; the Direct client surfaces
    /// them generically. Used by the Plugin-advantage suite.
    /// </summary>
    public static string PaymentRequiredProductHandle => Get("PAYMENT_REQUIRED_PRODUCT_HANDLE", "card-required");

    /// <summary>
    /// All product handles the mock treats as payment/card validation failures (each returns a <c>422</c>
    /// carrying at least one payment keyword, so the Plugin surfaces the typed
    /// <c>PaymentVerificationRequiredException</c> for every one). Comma-separated, overridable via
    /// <c>PAYMENT_REQUIRED_PRODUCT_HANDLES</c>. Superset of <see cref="PaymentRequiredProductHandle"/> (kept
    /// for back-compat). Used by the Plugin-advantage payment theory.
    /// </summary>
    public static IReadOnlyList<string> PaymentRequiredProductHandles =>
        Get("PAYMENT_REQUIRED_PRODUCT_HANDLES", "card-required,threeds-required,card-declined")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Get(string key, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
