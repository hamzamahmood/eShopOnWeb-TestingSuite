namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class RecordUsageRequest : BaseRequest
{
    /// <summary>Bound from the route; the subscription to record usage against.</summary>
    public int SubscriptionId { get; set; }

    /// <summary>The number of metered units consumed (must be positive).</summary>
    public int Quantity { get; set; }

    public string? Memo { get; set; }

    /// <summary>Set from the caller's JWT name claim in the route handler.</summary>
    public string? UserName { get; set; }

    /// <summary>Set from the caller's JWT role claim in the route handler.</summary>
    public bool IsAdministrator { get; set; }
}
