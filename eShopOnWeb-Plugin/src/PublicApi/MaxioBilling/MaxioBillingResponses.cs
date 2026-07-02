using System;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

namespace Microsoft.eShopWeb.PublicApi.MaxioBilling;

/// <summary>
/// Response shapes for <see cref="MaxioBillingController"/>. These mirror the provider-agnostic DTOs the
/// <c>IBillingClient</c> already returns (Maxio's own responses are flattened inside the client and never
/// reach this layer). Enum-valued fields are rendered as strings for readability, matching the convention
/// used by the app's own subscription endpoints.
/// </summary>
public record CustomerIdResponse(string CustomerId);

public record SubscriptionResponse(
    string SubscriptionId,
    string CustomerReference,
    string ProductHandle,
    string ProductName,
    decimal Price,
    string State,
    decimal Mrr,
    DateTimeOffset? NextAssessmentAt)
{
    public static SubscriptionResponse From(SubscriptionDto dto) => new(
        dto.SubscriptionId,
        dto.CustomerReference,
        dto.ProductHandle,
        dto.ProductName,
        dto.Price,
        dto.State.ToString(),
        dto.Mrr,
        dto.NextAssessmentAt);
}

public record PlanChangeQuoteResponse(
    string SubscriptionId,
    string FromProductHandle,
    string ToProductHandle,
    string Timing,
    decimal ProratedAmount,
    DateTimeOffset EffectiveAt)
{
    public static PlanChangeQuoteResponse From(PlanChangeQuoteDto dto) => new(
        dto.SubscriptionId,
        dto.FromProductHandle,
        dto.ToProductHandle,
        dto.Timing.ToString(),
        dto.ProratedAmount,
        dto.EffectiveAt);
}
