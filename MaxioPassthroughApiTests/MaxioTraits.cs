namespace MaxioPassthroughApiTests;

/// <summary>
/// Single source of truth for the xUnit <c>[Trait]</c> keys/values this suite attaches to every test, so an
/// executor (human, CI, or agent) can read which Maxio API operation a test targets straight from the run
/// results — without the suite losing its black-box character (it still only calls the PublicApi over HTTP).
///
/// <para>
/// xUnit traits do NOT appear in the default console output and are not reliably serialized to <c>.trx</c>.
/// They are filterable (<c>dotnet test --filter "Category=endpoint"</c>) and are reliably emitted as
/// <c>&lt;properties&gt;</c> by the <c>JunitXml.TestLogger</c> package — see the README for the exact command.
/// </para>
/// </summary>
public static class MaxioTraits
{
    // Trait keys
    public const string Api = "MaxioApi";
    public const string Category = "Category";

    // Category values — mirror the grouping in docs/test-cases-by-integration-*.md
    public const string CategoryEndpoint = "endpoint";
    public const string CategoryPluginAdvantage = "plugin-advantage";
    public const string CategorySafetyNet = "safety-net";

    // Maxio API operation signatures — verbatim "METHOD /path" from openAPI/openapi.yaml.
    public const string ListProducts = "GET /product_families/{product_family_id}/products.json";
    public const string LookupCustomer = "GET /customers/lookup.json";
    public const string CreateCustomer = "POST /customers.json";
    public const string ListCustomerSubs = "GET /customers/{customer_id}/subscriptions.json";
    public const string ReadSubscription = "GET /subscriptions/{subscription_id}.json";
    public const string CreateSubscription = "POST /subscriptions.json";
    public const string HoldSubscription = "POST /subscriptions/{subscription_id}/hold.json";
    public const string ResumeSubscription = "POST /subscriptions/{subscription_id}/resume.json";
    public const string ReactivateSub = "PUT /subscriptions/{subscription_id}/reactivate.json";
    public const string MigrateSubscription = "POST /subscriptions/{subscription_id}/migrations.json";
    public const string PreviewMigration = "POST /subscriptions/{subscription_id}/migrations/preview.json";
    public const string UpdateSubscription = "PUT /subscriptions/{subscription_id}.json";
    public const string CancelSubscription = "DELETE /subscriptions/{subscription_id}.json";
    public const string DelayedCancel = "POST /subscriptions/{subscription_id}/delayed_cancel.json";
    public const string ReadSubscriptionComponent = "GET /subscriptions/{subscription_id}/components/{component_id}.json";
    public const string FindComponent = "GET /components/lookup.json";
    public const string RecordUsage = "POST /subscriptions/{subscription_id_or_reference}/components/{component_id}/usages.json";
}
