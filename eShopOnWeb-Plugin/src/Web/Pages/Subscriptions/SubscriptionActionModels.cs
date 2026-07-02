using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.Web.Pages.Subscriptions;

public class SubscribeInput
{
    [Required]
    public string ProductHandle { get; set; } = string.Empty;
}

public class RecordUsageInput
{
    [Required]
    public string SubscriptionId { get; set; } = string.Empty;

    [Range(0.01, 1_000_000)]
    public decimal Quantity { get; set; } = 1;

    [StringLength(500)]
    public string? Memo { get; set; }
}

public class PreviewPlanChangeInput
{
    [Required]
    public string SubscriptionId { get; set; } = string.Empty;

    [Required]
    public string TargetProductHandle { get; set; } = string.Empty;

    [Required]
    public string Timing { get; set; } = string.Empty;
}

public class CommitPlanChangeInput
{
    [Required]
    public string SubscriptionId { get; set; } = string.Empty;

    [Required]
    public string PreviewToken { get; set; } = string.Empty;
}

public class LifecycleActionInput
{
    [Required]
    public string SubscriptionId { get; set; } = string.Empty;
}

public class CancelInput
{
    [Required]
    public string SubscriptionId { get; set; } = string.Empty;

    [Required]
    public string Timing { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Reason { get; set; }
}
