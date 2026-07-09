namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class LifecycleRequest : BaseRequest
{
    /// <summary>Bound from the route.</summary>
    public int SubscriptionId { get; set; }

    /// <summary>One of: Pause, Resume, Cancel, CancelAtEndOfPeriod, Reactivate.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Optional reason/message recorded with the transition (used for cancellations).</summary>
    public string? Reason { get; set; }
}
