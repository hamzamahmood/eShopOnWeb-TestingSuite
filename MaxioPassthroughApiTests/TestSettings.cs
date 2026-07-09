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
        (Environment.GetEnvironmentVariable("PUBLICAPI_BASEURL") ?? "http://localhost:5000").TrimEnd('/');

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

    /// <summary>Builds the preview-plan-change path (identical route shape on both integrations).</summary>
    public static string MigrationsPreviewPath(string subscriptionId) => $"{MigrationsPath(subscriptionId)}/preview";

    /// <summary>
    /// Builds the usage-summary / component-balance path. Routes genuinely differ between integrations, so —
    /// like <see cref="RecordUsagePath"/> — this is configurable via <c>USAGE_SUMMARY_PATH_TEMPLATE</c> (must
    /// contain a literal <c>{subscriptionId}</c> placeholder). Defaults to the Plugin form; set
    /// <c>USAGE_SUMMARY_PATH_TEMPLATE=/api/maxio/subscriptions/{{subscriptionId}}/component-balance</c> for Direct.
    /// </summary>
    public static string UsageSummaryPath(string subscriptionId) =>
        Get("USAGE_SUMMARY_PATH_TEMPLATE", "/api/maxio/subscriptions/{subscriptionId}/component-balance")
            .Replace("{subscriptionId}", subscriptionId);

    /// <summary>
    /// Path of the metered-component verify/read endpoint. Route AND success status diverge (Plugin
    /// <c>metered-component/verify</c> → 204 no body; Direct <c>metered-component</c> → 200 + data), so tests
    /// assert a 2xx range. Configurable via <c>METERED_COMPONENT_PATH</c>; defaults to the Plugin form. Set
    /// <c>METERED_COMPONENT_PATH=/api/maxio/metered-component</c> for Direct.
    /// </summary>
    public static string MeteredComponentPath => Get("METERED_COMPONENT_PATH", "/api/maxio/metered-component");

    /// <summary>
    /// Path of the metered-component read/validate endpoint as exposed by the run_3 integrations — both now
    /// serve <c>GET /api/maxio/metered-component</c> returning 200 + data (the older Plugin
    /// <c>/verify</c> → 204 form, still covered by <see cref="MeteredComponentPath"/>, no longer exists).
    /// Configurable via <c>METERED_COMPONENT_READ_PATH</c>.
    /// </summary>
    public static string MeteredComponentReadPath => Get("METERED_COMPONENT_READ_PATH", "/api/maxio/metered-component");

    /// <summary>
    /// Builds the cancel-at-end-of-period path. Route AND HTTP method diverge: the Plugin reuses
    /// <c>DELETE subscriptions/{id}</c> with a <c>timing:"EndOfPeriod"</c> body; the Direct integration has a
    /// dedicated <c>POST subscriptions/{id}/delayed_cancel</c>. Configure both via
    /// <c>END_OF_PERIOD_CANCEL_PATH_TEMPLATE</c> (with a <c>{subscriptionId}</c> placeholder) and
    /// <c>END_OF_PERIOD_CANCEL_METHOD</c>. Defaults to the Plugin form; for Direct set
    /// <c>END_OF_PERIOD_CANCEL_PATH_TEMPLATE=/api/maxio/subscriptions/{{subscriptionId}}/delayed_cancel</c> and
    /// <c>END_OF_PERIOD_CANCEL_METHOD=POST</c>.
    /// </summary>
    public static string EndOfPeriodCancelPath(string subscriptionId) =>
        Get("END_OF_PERIOD_CANCEL_PATH_TEMPLATE", "/api/maxio/subscriptions/{subscriptionId}")
            .Replace("{subscriptionId}", subscriptionId);

    /// <summary>HTTP method for the cancel-at-end-of-period endpoint (<c>DELETE</c> Plugin / <c>POST</c> Direct).</summary>
    public static string EndOfPeriodCancelMethod => Get("END_OF_PERIOD_CANCEL_METHOD", "DELETE");

    /// <summary>
    /// Builds the schedule-plan-change-at-renewal path. Route AND HTTP method diverge: the Plugin reuses
    /// <c>POST subscriptions/{id}/migrations</c> with a <c>timing:"AtRenewal"</c> body; the Direct integration
    /// has a dedicated <c>PUT subscriptions/{id}</c>. Configure both via
    /// <c>SCHEDULE_AT_RENEWAL_PATH_TEMPLATE</c> (with a <c>{subscriptionId}</c> placeholder) and
    /// <c>SCHEDULE_AT_RENEWAL_METHOD</c>. Defaults to the Plugin form; for Direct set
    /// <c>SCHEDULE_AT_RENEWAL_PATH_TEMPLATE=/api/maxio/subscriptions/{{subscriptionId}}</c> and
    /// <c>SCHEDULE_AT_RENEWAL_METHOD=PUT</c>.
    /// </summary>
    public static string ScheduleAtRenewalPath(string subscriptionId) =>
        Get("SCHEDULE_AT_RENEWAL_PATH_TEMPLATE", "/api/maxio/subscriptions/{subscriptionId}/migrations")
            .Replace("{subscriptionId}", subscriptionId);

    /// <summary>HTTP method for the schedule-at-renewal endpoint (<c>POST</c> Plugin / <c>PUT</c> Direct).</summary>
    public static string ScheduleAtRenewalMethod => Get("SCHEDULE_AT_RENEWAL_METHOD", "POST");

    /// <summary>A customer reference the mock knows (→ customer id 98765).</summary>
    public static string KnownCustomerReference => Get("KNOWN_CUSTOMER_REFERENCE", "cust_12345");

    /// <summary>
    /// The known customer's email (→ customer id 98765). A valid email address that the mock also accepts as a
    /// lookup key, for an integration that treats the email as the provider reference.
    /// </summary>
    public static string KnownCustomerEmail => Get("KNOWN_CUSTOMER_EMAIL", "john.doe@example.com");

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
    /// A non-numeric subscription id. A REST-correct API rejects it with a 400 client error; an integration
    /// whose route constrains the id to an integer instead route-misses (empty-body 404), which the suite
    /// treats as route divergence and skips.
    /// </summary>
    public static string NonNumericSubscriptionId => Get("NON_NUMERIC_SUBSCRIPTION_ID", "abc");

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

    // --- Persistent server-fault injection (robustness suite) ----------------------------------------------
    // Reserved subscription ids / product handles / customer-reference prefixes the mock answers with a
    // PERSISTENT upstream fault (every attempt fails — unlike the transient retry_/ratelimit_/connbreak_
    // references). They verify the integration layer turns a faulty Maxio (5xx, 429, malformed/empty body)
    // into a clean server error without crashing or leaking internals. See MaxioMockServer Program.cs.

    /// <summary>Subscription id the mock answers with a persistent <c>500</c> on every subscription-scoped route.</summary>
    public static string ServerError500SubscriptionId => Get("SERVER_ERROR_500_SUBSCRIPTION_ID", "59990500");

    /// <summary>Subscription id the mock answers with a persistent <c>503</c>.</summary>
    public static string ServerError503SubscriptionId => Get("SERVER_ERROR_503_SUBSCRIPTION_ID", "59990503");

    /// <summary>Subscription id the mock answers with a persistent <c>429</c> (rate limit, with Retry-After).</summary>
    public static string RateLimited429SubscriptionId => Get("RATE_LIMITED_429_SUBSCRIPTION_ID", "59990429");

    /// <summary>Subscription id the mock answers with a well-formed <c>200</c> carrying a MALFORMED (unparseable) JSON body.</summary>
    public static string MalformedBodySubscriptionId => Get("MALFORMED_BODY_SUBSCRIPTION_ID", "59990900");

    /// <summary>Subscription id the mock answers with a <c>200</c> and an EMPTY body.</summary>
    public static string EmptyBodySubscriptionId => Get("EMPTY_BODY_SUBSCRIPTION_ID", "59990204");

    /// <summary>Product handle the mock answers with a persistent <c>500</c> on create-subscription / migrate.</summary>
    public static string ServerError500ProductHandle => Get("SERVER_ERROR_500_PRODUCT_HANDLE", "server-error-500");

    /// <summary>Product handle the mock answers with a <c>200</c> carrying a MALFORMED body on create-subscription / migrate.</summary>
    public static string MalformedResponseProductHandle => Get("MALFORMED_RESPONSE_PRODUCT_HANDLE", "malformed-response");

    /// <summary>Builds a fresh customer reference the mock lookup answers with a persistent <c>500</c>.</summary>
    public static string NewServerError500Reference() => $"fault500_{Guid.NewGuid():N}";

    /// <summary>Builds a fresh customer reference the mock lookup answers with a persistent <c>503</c>.</summary>
    public static string NewServerError503Reference() => $"fault503_{Guid.NewGuid():N}";

    /// <summary>Builds a fresh customer reference the mock lookup answers with a persistent <c>429</c>.</summary>
    public static string NewRateLimited429Reference() => $"fault429_{Guid.NewGuid():N}";

    /// <summary>Builds a fresh customer reference the mock lookup answers with a <c>200</c> + malformed body.</summary>
    public static string NewMalformedBodyReference() => $"malformed_{Guid.NewGuid():N}";

    /// <summary>Builds a fresh customer reference the mock lookup answers with a <c>200</c> + empty body.</summary>
    public static string NewEmptyBodyReference() => $"emptybody_{Guid.NewGuid():N}";

    /// <summary>
    /// Builds a fresh customer reference whose create the mock rejects with the object-map error shape
    /// (<c>{ "errors": { "customer": "…" } }</c>), the alternate form in Customer-Error-Response's oneOf.
    /// </summary>
    public static string NewObjectMapErrorReference() => $"objmaperr_{Guid.NewGuid():N}";

    /// <summary>A known customer id the mock answers with an EMPTY subscriptions array (a valid success variant).</summary>
    public static string EmptySubscriptionsCustomerId => Get("EMPTY_SUBSCRIPTIONS_CUSTOMER_ID", "98700");

    // --- AI payload verification (content-comparison tests only) -------------------------------------------
    // Off by default so key-less / CI runs skip the AI-backed content tests instead of failing. See Ai/.

    /// <summary>
    /// Master switch for AI payload verification (<c>AI_COMPARISON_ENABLED</c>, default <b>on</b>). Override to
    /// <c>false</c> to force the content-comparison tests to skip. (With no API key configured the tests skip
    /// regardless — see <see cref="AiApiKey"/> — so key-less/CI runs are unaffected.)
    /// </summary>
    public static bool AiComparisonEnabled =>
        !bool.TryParse(Get("AI_COMPARISON_ENABLED", "true"), out var b) || b;

    /// <summary>
    /// API key for the OpenAI (or OpenAI-compatible) endpoint. Prefers <c>AI_API_KEY</c>, then falls back to the
    /// conventional <c>OPENAI_API_KEY</c>. Empty ⇒ AI comparison disabled.
    /// </summary>
    public static string AiApiKey => Get("AI_API_KEY", "");

    /// <summary>
    /// Chat model / deployment name to verify with (<c>AI_MODEL</c>). Defaults to <c>gpt-5.5</c>.
    /// </summary>
    public static string AiModel => Get("AI_MODEL", "gpt-5.5");

    /// <summary>Optional OpenAI-compatible base URL override (<c>AI_ENDPOINT</c>). Empty ⇒ OpenAI cloud.</summary>
    public static string AiEndpoint => Get("AI_ENDPOINT", "");

    /// <summary>
    /// Whether to constrain the response with a native JSON schema (<c>AI_USE_JSON_SCHEMA</c>, default true).
    /// Set false for OpenAI-compatible endpoints that don't support schema-based structured output.
    /// </summary>
    public static bool AiUseJsonSchema =>
        !bool.TryParse(Get("AI_USE_JSON_SCHEMA", "true"), out var b) || b;

    private static string Get(string key, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
