namespace MaxioApiTests;

/// <summary>Test trait constants for categorizing and filtering tests.</summary>
public static class MaxioTraits
{
    public const string Api = "MaxioApi";
    public const string Category = "Category";
    public const string CategoryEndpoint = "endpoint";
    public const string CategorySafetyNet = "safety-net";
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
