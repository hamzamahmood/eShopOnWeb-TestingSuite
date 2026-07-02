using System;

namespace Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

/// <summary>
/// A customer's subscription as read back from the billing provider. Provider-agnostic — no Maxio SDK types.
/// </summary>
public record SubscriptionDto(
    string SubscriptionId,
    string CustomerReference,
    string ProductHandle,
    string ProductName,
    decimal Price,
    SubscriptionState State,
    decimal Mrr,
    DateTimeOffset? NextAssessmentAt);
