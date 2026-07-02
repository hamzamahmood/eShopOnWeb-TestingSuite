using MediatR;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

/// <summary>
/// Published in-process after a plan change (UC3) commits.
/// </summary>
public class SubscriptionPlanChanged : INotification
{
    public string CustomerReference { get; }
    public string SubscriptionId { get; }
    public string OldProductHandle { get; }
    public string NewProductHandle { get; }
    public decimal ProratedAmount { get; }

    public SubscriptionPlanChanged(string customerReference, string subscriptionId, string oldProductHandle, string newProductHandle, decimal proratedAmount)
    {
        CustomerReference = customerReference;
        SubscriptionId = subscriptionId;
        OldProductHandle = oldProductHandle;
        NewProductHandle = newProductHandle;
        ProratedAmount = proratedAmount;
    }
}
