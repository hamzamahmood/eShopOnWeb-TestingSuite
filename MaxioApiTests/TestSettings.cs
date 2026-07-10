namespace MaxioApiTests;

/// <summary>Test configuration from environment variables.</summary>
public static class TestSettings
{
    /// <summary>Base URL of the API under test.</summary>
    public static string BaseUrl =>
        (Environment.GetEnvironmentVariable("PUBLICAPI_BASEURL") ?? "http://localhost:5000").TrimEnd('/');

    public static string ListPlansPath =>
        Get("LIST_PLANS_PATH", "/api/maxio/product-families/527890/products");

    public static string CustomerSubscriptionsPath(string customerId) =>
        $"/api/maxio/customers/{customerId}/subscriptions";

    public static string CustomersPath => "/api/maxio/customers";

    public static string CustomerLookupPath(string reference) =>
        $"/api/maxio/customers/lookup?reference={Uri.EscapeDataString(reference)}";

    public static string SubscriptionsPath => "/api/maxio/subscriptions";

    public static string SubscriptionPath(string subscriptionId) => $"/api/maxio/subscriptions/{subscriptionId}";

    public static string PauseSubscriptionPath(string subscriptionId) => $"{SubscriptionPath(subscriptionId)}/hold";

    public static string ResumeSubscriptionPath(string subscriptionId) => $"{SubscriptionPath(subscriptionId)}/resume";

    public static string ReactivateSubscriptionPath(string subscriptionId) => $"{SubscriptionPath(subscriptionId)}/reactivate";

    public static string MigrationsPath(string subscriptionId) => $"{SubscriptionPath(subscriptionId)}/migrations";

    public static string RecordUsagePath(string subscriptionId) =>
        Get("RECORD_USAGE_PATH_TEMPLATE", "/api/maxio/subscriptions/{subscriptionId}/components/1/usages")
            .Replace("{subscriptionId}", subscriptionId);

    public static string MigrationsPreviewPath(string subscriptionId) => $"{MigrationsPath(subscriptionId)}/preview";

    public static string UsageSummaryPath(string subscriptionId) =>
        Get("USAGE_SUMMARY_PATH_TEMPLATE", "/api/maxio/subscriptions/{subscriptionId}/component-balance")
            .Replace("{subscriptionId}", subscriptionId);

    public static string MeteredComponentPath => Get("METERED_COMPONENT_PATH", "/api/maxio/metered-component");

    public static string MeteredComponentReadPath => Get("METERED_COMPONENT_READ_PATH", "/api/maxio/metered-component");

    public static string EndOfPeriodCancelPath(string subscriptionId) =>
        Get("END_OF_PERIOD_CANCEL_PATH_TEMPLATE", "/api/maxio/subscriptions/{subscriptionId}")
            .Replace("{subscriptionId}", subscriptionId);

    public static string EndOfPeriodCancelMethod => Get("END_OF_PERIOD_CANCEL_METHOD", "DELETE");

    public static string ScheduleAtRenewalPath(string subscriptionId) =>
        Get("SCHEDULE_AT_RENEWAL_PATH_TEMPLATE", "/api/maxio/subscriptions/{subscriptionId}/migrations")
            .Replace("{subscriptionId}", subscriptionId);

    public static string ScheduleAtRenewalMethod => Get("SCHEDULE_AT_RENEWAL_METHOD", "POST");

    public static string KnownCustomerReference => Get("KNOWN_CUSTOMER_REFERENCE", "cust_12345");

    public static string KnownCustomerEmail => Get("KNOWN_CUSTOMER_EMAIL", "john.doe@example.com");

    public static string KnownCustomerId => Get("KNOWN_CUSTOMER_ID", "98765");

    public static string UnknownCustomerReference => Get("UNKNOWN_CUSTOMER_REFERENCE", "no_such_customer_ref");

    public static string UnknownCustomerId => Get("UNKNOWN_CUSTOMER_ID", "99999999");

    /// <summary>Builds a fresh, never-before-seen customer reference for a find-or-create-customer test run.</summary>
    public static string NewCustomerReference() => $"newcust_{Guid.NewGuid():N}";

    public static string KnownActiveSubscriptionId => Get("KNOWN_ACTIVE_SUBSCRIPTION_ID", "15100121");

    public static string KnownOnHoldSubscriptionId => Get("KNOWN_ON_HOLD_SUBSCRIPTION_ID", "15100210");

    public static string KnownCanceledSubscriptionId => Get("KNOWN_CANCELED_SUBSCRIPTION_ID", "15100299");

    /// <summary>A well-formed but unknown numeric subscription id.</summary>
    public static string UnknownSubscriptionId => Get("UNKNOWN_SUBSCRIPTION_ID", "88888888");

    public static string NonNumericSubscriptionId => Get("NON_NUMERIC_SUBSCRIPTION_ID", "abc");

    public static string UnknownStateSubscriptionId => Get("UNKNOWN_STATE_SUBSCRIPTION_ID", "15100377");

    public static string KnownProductHandle => Get("KNOWN_PRODUCT_HANDLE", "gold");

    /// <summary>A second known product handle used as a migration target.</summary>
    public static string AlternateProductHandle => Get("ALTERNATE_PRODUCT_HANDLE", "zero-dollar-product");

    /// <summary>A well-formed but unknown product handle.</summary>
    public static string UnknownProductHandle => Get("UNKNOWN_PRODUCT_HANDLE", "no-such-plan");

    public static string Transient5xxReferencePrefix => Get("TRANSIENT_5XX_REFERENCE_PREFIX", "retry_");

    public static string RateLimitReferencePrefix => Get("RATE_LIMIT_REFERENCE_PREFIX", "ratelimit_");

    /// <summary>Builds a fresh transient-<c>503</c> reference with a unique nonce for a single test run.</summary>
    public static string NewTransient5xxReference() => $"{Transient5xxReferencePrefix}{Guid.NewGuid():N}";

    /// <summary>Builds a fresh <c>429</c> rate-limit reference with a unique nonce for a single test run.</summary>
    public static string NewRateLimitReference() => $"{RateLimitReferencePrefix}{Guid.NewGuid():N}";

    public static string ConnectionInterruptReferencePrefix => Get("CONNECTION_INTERRUPT_REFERENCE_PREFIX", "connbreak_");

    /// <summary>Builds a fresh connection-interruption reference with a unique nonce for a single test run.</summary>
    public static string NewConnectionInterruptReference() => $"{ConnectionInterruptReferencePrefix}{Guid.NewGuid():N}";

    public static string RaceReferencePrefix => Get("RACE_REFERENCE_PREFIX", "race_");

    /// <summary>Builds a fresh concurrent-create-race reference with a unique nonce for a single test run.</summary>
    public static string NewRaceReference() => $"{RaceReferencePrefix}{Guid.NewGuid():N}";

    public static string PaymentRequiredProductHandle => Get("PAYMENT_REQUIRED_PRODUCT_HANDLE", "card-required");

    public static IReadOnlyList<string> PaymentRequiredProductHandles =>
        Get("PAYMENT_REQUIRED_PRODUCT_HANDLES", "card-required,threeds-required,card-declined")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string ServerError500SubscriptionId => Get("SERVER_ERROR_500_SUBSCRIPTION_ID", "59990500");

    public static string ServerError503SubscriptionId => Get("SERVER_ERROR_503_SUBSCRIPTION_ID", "59990503");

    public static string RateLimited429SubscriptionId => Get("RATE_LIMITED_429_SUBSCRIPTION_ID", "59990429");

    public static string MalformedBodySubscriptionId => Get("MALFORMED_BODY_SUBSCRIPTION_ID", "59990900");

    public static string EmptyBodySubscriptionId => Get("EMPTY_BODY_SUBSCRIPTION_ID", "59990204");

    public static string ServerError500ProductHandle => Get("SERVER_ERROR_500_PRODUCT_HANDLE", "server-error-500");

    public static string MalformedResponseProductHandle => Get("MALFORMED_RESPONSE_PRODUCT_HANDLE", "malformed-response");

    public static string NewServerError500Reference() => $"fault500_{Guid.NewGuid():N}";

    public static string NewServerError503Reference() => $"fault503_{Guid.NewGuid():N}";

    public static string NewRateLimited429Reference() => $"fault429_{Guid.NewGuid():N}";

    public static string NewMalformedBodyReference() => $"malformed_{Guid.NewGuid():N}";

    public static string NewEmptyBodyReference() => $"emptybody_{Guid.NewGuid():N}";

    public static string NewObjectMapErrorReference() => $"objmaperr_{Guid.NewGuid():N}";

    public static string EmptySubscriptionsCustomerId => Get("EMPTY_SUBSCRIPTIONS_CUSTOMER_ID", "98700");

    /// <summary>
    /// Master switch for AI payload verification (<c>AI_COMPARISON_ENABLED</c>, default <b>on</b>). Override to
    /// <c>false</c> to force the content-comparison tests to skip. (With no API key configured the tests skip
    /// regardless — see <see cref="AiApiKey"/> — so key-less/CI runs are unaffected.)
    /// </summary>
    public static bool AiComparisonEnabled =>
        !bool.TryParse(Get("AI_COMPARISON_ENABLED", "true"), out var b) || b;

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
