namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class SubscribeRequest : BaseRequest
{
    /// <summary>The handle of the plan to subscribe to (e.g. <c>eshop-pro</c>).</summary>
    public string ProductHandle { get; set; } = string.Empty;

    /// <summary>Set from the caller's JWT name claim in the route handler.</summary>
    public string? UserName { get; set; }
}
