namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

/// <summary>
/// Outcome of recording pay-as-you-go usage against a subscription's metered component (UC2).
/// The usage is billed on the next renewal invoice.
/// </summary>
public class UsageResult
{
    /// <summary>The quantity that was just recorded.</summary>
    public int RecordedQuantity { get; init; }

    public string? Memo { get; init; }

    /// <summary>
    /// The running period-to-date total of billable units for the component, if it could be read back.
    /// Null when the record succeeded but the read-back of the running total failed
    /// (the usage still stands — see UC2 failure scenarios).
    /// </summary>
    public int? PeriodToDateTotal { get; init; }
}
