namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

// Result of recording pay-as-you-go usage (UC2). The period-to-date total is
// read back after recording; if that read-back fails the usage still stands and
// the total is reported as unavailable rather than failing the whole operation.
public record UsageSummary
{
    public int SubscriptionId { get; init; }
    public int RecordedQuantity { get; init; }
    public int? PeriodToDateTotal { get; init; }
    public string? Memo { get; init; }

    public bool PeriodTotalAvailable => PeriodToDateTotal.HasValue;
}
