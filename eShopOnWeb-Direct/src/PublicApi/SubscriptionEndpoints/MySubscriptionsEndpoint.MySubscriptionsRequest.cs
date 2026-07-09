namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class MySubscriptionsRequest : BaseRequest
{
    /// <summary>Set from the caller's JWT name claim in the route handler.</summary>
    public string? UserName { get; set; }
}
