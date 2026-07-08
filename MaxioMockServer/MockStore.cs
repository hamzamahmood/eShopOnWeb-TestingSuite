using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Text.Json.Nodes;

namespace MaxioMockServer;

/// <summary>
/// Holds the canned response payloads (loaded once at startup) and the set of
/// "known" identifiers that map to a successful response. Anything not in these
/// sets is treated as not-found (404), matching the real Maxio API behavior.
/// </summary>
public sealed class MockStore
{
    /// <summary>Raw JSON for <c>GET /product_families/{id}/products.json</c>.</summary>
    public string ProductsJson { get; }

    /// <summary>Raw JSON for <c>GET /customers/lookup.json</c>.</summary>
    public string CustomerJson { get; }

    /// <summary>Raw JSON for <c>GET /customers/{id}/subscriptions.json</c>.</summary>
    public string SubscriptionsJson { get; }

    /// <summary>Raw JSON for <c>GET /product_families/{id}/components/{component_id}.json</c>.</summary>
    public string ComponentJson { get; }

    /// <summary>
    /// Single-subscription envelopes (<c>{"subscription": {...}}</c>) for <c>GET /subscriptions/{id}.json</c>
    /// and as the base body every subscription-lifecycle action (pause/resume/reactivate/migrate/cancel)
    /// patches its result from, keyed by subscription id. Three canonical states are seeded so every
    /// lifecycle transition has a realistic "from" state to act on:
    ///   * 15100121 - active (the same subscription surfaced by <see cref="SubscriptionsJson"/>)
    ///   * 15100210 - on_hold
    ///   * 15100299 - canceled
    ///   * 15100377 - "assessing" (a plausible-but-unknown provider state, not in either integration's known
    ///     list — used only via the read route to show the Plugin maps drift to a safe default while Direct
    ///     forwards the raw string; lifecycle actions gate on the three canonical ids, so hold/resume/
    ///     reactivate/migrate return wrong-state 422s for this id — untested and harmless on the stateless mock)
    /// </summary>
    public FrozenDictionary<int, string> SubscriptionsById { get; }

    /// <summary>Product family ids/handles that return the canned product list.</summary>
    public FrozenSet<string> KnownProductFamilyIds { get; } =
        new[] { "527890", "handle:acme-projects" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Customer <c>reference</c> values that resolve to the canned customer.</summary>
    public FrozenSet<string> KnownCustomerReferences { get; } =
        new[] { "cust_12345" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Customer ids that return the canned subscription list.</summary>
    public FrozenSet<int> KnownCustomerIds { get; } =
        new[] { 98765 }.ToFrozenSet();

    /// <summary>Product handles known to the configured product family (see <c>MockData/products.json</c>).</summary>
    public FrozenSet<string> KnownProductHandles { get; } =
        new[] { "zero-dollar-product", "gold" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Maps a known product handle to its canned display name, for migrate/create-subscription responses.</summary>
    public FrozenDictionary<string, (int Id, string Name, int PriceInCents)> ProductsByHandle { get; } =
        new Dictionary<string, (int, string, int)>
        {
            ["gold"] = (3858146, "Gold Plan", 1000),
            ["zero-dollar-product"] = (3801242, "Free product", 0)
        }.ToFrozenDictionary();

    /// <summary>Route tokens (id or <c>handle:</c>-prefixed handle) that resolve to the one configured metered component.</summary>
    public FrozenSet<string> KnownComponentTokens { get; } =
        new[] { "641814", "handle:api-calls" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Per-key attempt counter for the comparison-harness "transient failure" behaviors (references
    /// starting with <c>retry_</c> or <c>ratelimit_</c>). The first request for a given key returns a
    /// transient error (503 / 429); a retried request succeeds. Keyed by the full reference so each test
    /// run — which appends a fresh nonce — is independent of every other run's state, making the
    /// demonstration robust to test ordering.
    /// </summary>
    private readonly ConcurrentDictionary<string, int> _attempts = new(StringComparer.Ordinal);

    /// <summary>Records another attempt for <paramref name="key"/> and returns the new attempt number (1-based).</summary>
    public int NextAttempt(string key) => _attempts.AddOrUpdate(key, 1, (_, current) => current + 1);

    private MockStore(string productsJson, string customerJson, string subscriptionsJson, string componentJson, FrozenDictionary<int, string> subscriptionsById)
    {
        ProductsJson = productsJson;
        CustomerJson = customerJson;
        SubscriptionsJson = subscriptionsJson;
        ComponentJson = componentJson;
        SubscriptionsById = subscriptionsById;
    }

    /// <summary>
    /// Loads the canned JSON files from the <c>MockData</c> folder that sits next
    /// to the running assembly (copied there by the build).
    /// </summary>
    public static MockStore Load(string contentRootPath)
    {
        var dir = Path.Combine(contentRootPath, "MockData");

        string Read(string fileName)
        {
            var path = Path.Combine(dir, fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Required mock data file was not found: {path}", path);
            }
            return File.ReadAllText(path);
        }

        var subscriptionsById = new Dictionary<int, string>
        {
            [15100121] = Read("subscription-active.json"),
            [15100210] = Read("subscription-on-hold.json"),
            [15100299] = Read("subscription-canceled.json"),
            [15100377] = Read("subscription-unknown-state.json")
        }.ToFrozenDictionary();

        return new MockStore(
            productsJson: Read("products.json"),
            customerJson: Read("customer.json"),
            subscriptionsJson: Read("subscriptions.json"),
            componentJson: Read("component.json"),
            subscriptionsById: subscriptionsById);
    }

    /// <summary>
    /// Returns a copy of a canned subscription envelope with <c>subscription.state</c> replaced (and, for
    /// cancel/reactivate, <c>canceled_at</c> set/cleared to match) - the standard shape every mutating
    /// subscription-lifecycle action (pause/resume/reactivate/cancel) returns on success. The mock has no
    /// real state machine; this only patches the handful of fields a caller would plausibly assert on.
    /// </summary>
    public static string WithState(string subscriptionJson, string newState, string? canceledAt = null)
    {
        var root = JsonNode.Parse(subscriptionJson)!.AsObject();
        var subscription = root["subscription"]!.AsObject();
        var previousState = subscription["state"]?.GetValue<string>();
        subscription["state"] = newState;
        subscription["previous_state"] = previousState;
        subscription["canceled_at"] = canceledAt;
        return root.ToJsonString();
    }

    /// <summary>
    /// Returns a copy of the active subscription's envelope with <c>subscription.product</c> swapped to
    /// <paramref name="productHandle"/> - the shape <c>migrateSubscriptionProduct</c> returns on success.
    /// </summary>
    public string WithProduct(string subscriptionJson, string productHandle)
    {
        var (id, name, priceInCents) = ProductsByHandle[productHandle];
        var root = JsonNode.Parse(subscriptionJson)!.AsObject();
        var subscription = root["subscription"]!.AsObject();
        var product = subscription["product"]!.AsObject();
        product["id"] = id;
        product["name"] = name;
        product["handle"] = productHandle;
        product["price_in_cents"] = priceInCents;
        subscription["product_price_in_cents"] = priceInCents;
        return root.ToJsonString();
    }

    /// <summary>Builds a freshly "created" subscription envelope for <c>createSubscription</c> (POST /subscriptions.json).</summary>
    public string NewSubscriptionJson(int newSubscriptionId, int customerId, string productHandle)
    {
        var (id, name, priceInCents) = ProductsByHandle[productHandle];
        var root = JsonNode.Parse(SubscriptionsById[15100121])!.AsObject();
        var subscription = root["subscription"]!.AsObject();
        subscription["id"] = newSubscriptionId;
        subscription["state"] = "active";
        subscription["customer"]!["id"] = customerId;
        var product = subscription["product"]!.AsObject();
        product["id"] = id;
        product["name"] = name;
        product["handle"] = productHandle;
        product["price_in_cents"] = priceInCents;
        return root.ToJsonString();
    }

    /// <summary>Builds the <c>{"usage": {...}}</c> envelope for a successful <c>createUsage</c> call.</summary>
    public static string NewUsageJson(long usageId, int subscriptionId, decimal quantity, string? memo) =>
        new JsonObject
        {
            ["usage"] = new JsonObject
            {
                ["id"] = usageId,
                ["memo"] = memo,
                ["created_at"] = "2026-07-02T12:00:00-04:00",
                ["price_point_id"] = 555001,
                ["quantity"] = quantity,
                ["component_id"] = 641814,
                ["component_handle"] = "api-calls",
                ["subscription_id"] = subscriptionId
            }
        }.ToJsonString();

    /// <summary>
    /// Builds the <c>{"migration": {...}}</c> proration-preview envelope for a successful
    /// <c>previewSubscriptionProductMigration</c> call (POST /subscriptions/{id}/migrations/preview.json).
    /// Both integrations read these <c>*_in_cents</c> fields; the Direct client surfaces them as-is while the
    /// Plugin collapses them into a single <c>proratedAmount</c>.
    /// </summary>
    public static string MigrationPreviewJson() =>
        new JsonObject
        {
            ["migration"] = new JsonObject
            {
                ["prorated_adjustment_in_cents"] = 1000,
                ["charge_in_cents"] = 1000,
                ["payment_due_in_cents"] = 1000,
                ["credit_applied_in_cents"] = 0
            }
        }.ToJsonString();

    /// <summary>
    /// Builds the <c>{"component": {...}}</c> envelope for a successful <c>readSubscriptionComponent</c> call
    /// (GET /subscriptions/{id}/components/{component_id}.json) — the subscription-scoped component balance
    /// behind the usage-summary / component-balance endpoints. The Direct client reads only
    /// <c>unit_balance</c>; the Plugin also reads <c>component_handle</c>.
    /// </summary>
    public static string SubscriptionComponentJson(int subscriptionId, int unitBalance) =>
        new JsonObject
        {
            ["component"] = new JsonObject
            {
                ["component_id"] = 641814,
                ["subscription_id"] = subscriptionId,
                ["name"] = "API Calls",
                ["kind"] = "metered_component",
                ["unit_name"] = "call",
                ["unit_balance"] = unitBalance,
                ["component_handle"] = "api-calls",
                ["enabled"] = true,
                ["price_point_id"] = 555001,
                ["created_at"] = "2026-07-01T10:30:00-04:00",
                ["updated_at"] = "2026-07-01T10:30:00-04:00",
                ["archived_at"] = null
            }
        }.ToJsonString();

    /// <summary>
    /// Builds the <c>{"message": "..."}</c> envelope Maxio returns from <c>initiateDelayedCancellation</c>
    /// (POST /subscriptions/{id}/delayed_cancel.json). The Direct client discards this body and re-reads the
    /// subscription, so only HTTP 200 matters here.
    /// </summary>
    public static string DelayedCancelJson() =>
        new JsonObject
        {
            ["message"] = "This subscription will be canceled at the end of the current period."
        }.ToJsonString();

    /// <summary>Builds the <c>{"customer": {...}}</c> envelope for a successful <c>createCustomer</c> call (a not-yet-known reference).</summary>
    public static string NewCustomerJson(int customerId, string reference, string? email, string? firstName, string? lastName) =>
        new JsonObject
        {
            ["customer"] = new JsonObject
            {
                ["id"] = customerId,
                ["reference"] = reference,
                ["first_name"] = string.IsNullOrWhiteSpace(firstName) ? "eShopOnWeb" : firstName,
                ["last_name"] = string.IsNullOrWhiteSpace(lastName) ? "Customer" : lastName,
                ["email"] = email,
                ["organization"] = "",
                ["created_at"] = "2026-07-02T12:00:00-04:00",
                ["updated_at"] = "2026-07-02T12:00:00-04:00",
                ["verified"] = false,
                ["tax_exempt"] = false,
                ["vat_number"] = null,
                ["parent_id"] = null
            }
        }.ToJsonString();
}
