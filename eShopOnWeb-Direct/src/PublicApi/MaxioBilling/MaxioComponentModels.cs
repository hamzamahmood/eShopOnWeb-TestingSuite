using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.MaxioBilling;

/// <summary>Mirrors Create-Usage.yaml - the `usage` object of the createUsage body.</summary>
public sealed class CreateUsageModel
{
    /// <summary>FORWARDED. Integer by default, or decimal when fractional quantities are enabled.</summary>
    [JsonPropertyName("quantity")] public decimal? Quantity { get; set; }
    /// <summary>NOT forwarded - the client always records against the configured metered component's default price point.</summary>
    [JsonPropertyName("price_point_id")] public string? PricePointId { get; set; }
    /// <summary>FORWARDED as the usage memo.</summary>
    [JsonPropertyName("memo")] public string? Memo { get; set; }
    [JsonPropertyName("billing_schedule")] public BillingScheduleModel? BillingSchedule { get; set; }
    [JsonPropertyName("custom_price")] public ComponentCustomPriceModel? CustomPrice { get; set; }
}

/// <summary>Mirrors Create-Usage-Request.yaml (POST .../usages.json body).</summary>
public sealed class CreateUsageRequest
{
    [JsonPropertyName("usage")] public CreateUsageModel Usage { get; set; } = new();
}

/// <summary>
/// Query parameters of listProductsForProductFamily. Both fields mirror the spec but are NOT
/// forwarded - the client fetches the full product list for the configured product family.
/// </summary>
public sealed class ListProductsQuery
{
    /// <summary>Page number (spec default 1). NOT forwarded.</summary>
    [FromQuery(Name = "page")] public int? Page { get; set; }
    /// <summary>Results per page (spec default 20, max 200). NOT forwarded.</summary>
    [FromQuery(Name = "per_page")] public int? PerPage { get; set; }
}

/// <summary>
/// Query parameters of readSubscription. Mirrors the `include[]` param; NOT forwarded (the client
/// returns the base subscription only).
/// </summary>
public sealed class GetSubscriptionQuery
{
    /// <summary>Spec enum values: coupons, self_service_page_token. NOT forwarded.</summary>
    [FromQuery(Name = "include")] public string[]? Include { get; set; }
}

/// <summary>
/// Query parameters of resumeSubscription. Mirrors the calendar-billing resumption charge param
/// (spec key: calendar_billing['resumption_charge']); NOT forwarded (the client issues a plain resume).
/// </summary>
public sealed class ResumeSubscriptionQuery
{
    /// <summary>Reactivation-Charge enum: prorated (default) | immediate | delayed. NOT forwarded.</summary>
    [FromQuery(Name = "resumption_charge")] public string? ResumptionCharge { get; set; }
}
