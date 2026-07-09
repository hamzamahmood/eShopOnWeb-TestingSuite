using MediatR;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

// Published in-process (best-effort) after a subscription's plan is changed (UC3).
public class SubscriptionPlanChanged : INotification
{
    public SubscriptionPlanChanged(int subscriptionId, string fromPlanHandle, string toPlanHandle, bool appliedNow)
    {
        SubscriptionId = subscriptionId;
        FromPlanHandle = fromPlanHandle;
        ToPlanHandle = toPlanHandle;
        AppliedNow = appliedNow;
    }

    public int SubscriptionId { get; }
    public string FromPlanHandle { get; }
    public string ToPlanHandle { get; }
    public bool AppliedNow { get; }
}
