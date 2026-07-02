namespace Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

/// <summary>
/// A recurring plan (Maxio product) available for subscription. Provider-agnostic — no Maxio SDK types.
/// </summary>
public record PlanDto(
    string Handle,
    string Name,
    decimal Price,
    int IntervalCount,
    string IntervalUnit,
    bool RequiresPaymentMethod);
