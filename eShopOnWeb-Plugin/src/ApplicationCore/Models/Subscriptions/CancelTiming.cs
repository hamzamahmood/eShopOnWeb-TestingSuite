namespace Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

/// <summary>
/// When a cancel (UC4) should take effect.
/// </summary>
public enum CancelTiming
{
    Immediate,
    EndOfPeriod
}
