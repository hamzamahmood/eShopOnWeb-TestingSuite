using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.PublicApi.BillingEndpoints;

// Request/response contracts for MaxioBillingController. These are the microservice's public wire shapes;
// they are intentionally separate from both the ApplicationCore DTOs (returned directly on success) and
// the Maxio SDK models (which never leave Infrastructure). [Required]/[EmailAddress] are enforced
// automatically by [ApiController] model validation, which returns a 400 ValidationProblemDetails before
// the action body runs.

/// <summary>Body for <c>POST /api/billing/customers</c> (find-or-create a provider customer).</summary>
public sealed class FindOrCreateCustomerRequest
{
    /// <summary>The stable eShopOnWeb user id used as the provider customer reference.</summary>
    [Required]
    public string CustomerReference { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    public string? FirstName { get; init; }

    public string? LastName { get; init; }
}

/// <summary>Body for <c>POST /api/billing/subscriptions</c> (enroll a customer in a plan).</summary>
public sealed class CreateSubscriptionRequest
{
    /// <summary>Provider-side customer id (as returned by the customer endpoints), not the reference.</summary>
    [Required]
    public string ProviderCustomerId { get; init; } = string.Empty;

    [Required]
    public string ProductHandle { get; init; } = string.Empty;
}

/// <summary>Body for <c>POST /api/billing/subscriptions/{id}/usage</c> (record metered usage).</summary>
public sealed class RecordUsageRequest
{
    [Required]
    public decimal Quantity { get; init; }

    public string? Memo { get; init; }
}

/// <summary>
/// Body for the plan-change preview and commit endpoints. <see cref="Timing"/> accepts the case-insensitive
/// names of <c>PlanChangeTiming</c> (<c>Immediate</c> or <c>AtRenewal</c>); the controller validates it and
/// returns a 400 with the allowed values if it does not match.
/// </summary>
public sealed class PlanChangeRequest
{
    [Required]
    public string TargetProductHandle { get; init; } = string.Empty;

    [Required]
    public string Timing { get; init; } = string.Empty;
}

/// <summary>
/// Body for <c>POST /api/billing/subscriptions/{id}/cancel</c>. <see cref="Timing"/> accepts the
/// case-insensitive names of <c>CancelTiming</c> (<c>Immediate</c> or <c>EndOfPeriod</c>).
/// </summary>
public sealed class CancelSubscriptionRequest
{
    [Required]
    public string Timing { get; init; } = string.Empty;

    public string? Reason { get; init; }
}

/// <summary>Response for the customer lookup / find-or-create endpoints.</summary>
public sealed record CustomerIdResponse(string Reference, string CustomerId);
