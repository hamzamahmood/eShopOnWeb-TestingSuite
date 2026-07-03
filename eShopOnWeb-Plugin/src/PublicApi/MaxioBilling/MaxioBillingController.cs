using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

namespace Microsoft.eShopWeb.PublicApi.MaxioBilling;

/// <summary>
/// Exposes <see cref="IBillingClient"/> (the Maxio Advanced Billing seam) as a microservice: one endpoint
/// per client method. Each endpoint's request contract is shaped after the corresponding Maxio API
/// operation (snake_case fields, wrapper envelopes, route/query/body placement). Only the fields the client
/// method can actually forward are honored; every other documented Maxio field is accepted for contract
/// fidelity and silently ignored (see the request DTOs).
///
/// Error handling: provider/domain failures are thrown by the client as ApplicationCore exceptions and
/// mapped to HTTP status codes by the global <c>ExceptionMiddleware</c> (SubscriptionNotFound → 404,
/// PaymentVerificationRequired → 422, MeteredComponentMisconfigured/BillingProvider → 422). This controller
/// only adds the input handling the middleware cannot: numeric-id validation and the null-means-absent case.
///
/// Auth: <see cref="AllowAnonymousAttribute"/> is deliberate, matching <c>MaxioPassthroughController</c>.
/// </summary>
[ApiController]
[AllowAnonymous]
[Produces("application/json")]
[Route("api/maxio")]
[ApiExplorerSettings(GroupName = "v1")]
public class MaxioBillingController : ControllerBase
{
    private readonly IBillingClient _billingClient;

    public MaxioBillingController(IBillingClient billingClient)
    {
        _billingClient = billingClient;
    }

    /// <summary>
    /// GET /product_families/{product_family_id}/products.json — lists plans.
    /// The path id and all query params are inert: the client always uses the configured product family and
    /// filters out archived products itself.
    /// </summary>
    [HttpGet("product-families/{productFamilyId}/products")]
    [ProducesResponseType(typeof(IReadOnlyList<PlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPlans(
        string productFamilyId,
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "per_page")] int? perPage,
        [FromQuery(Name = "date_field")] string? dateField,
        [FromQuery(Name = "filter")] Dictionary<string, string>? filter,
        [FromQuery(Name = "start_date")] string? startDate,
        [FromQuery(Name = "end_date")] string? endDate,
        [FromQuery(Name = "start_datetime")] string? startDatetime,
        [FromQuery(Name = "end_datetime")] string? endDatetime,
        [FromQuery(Name = "include_archived")] bool? includeArchived,
        [FromQuery(Name = "include")] string? include,
        CancellationToken cancellationToken)
    {
        var plans = await _billingClient.ListPlansAsync(cancellationToken);
        return Ok(plans);
    }

    /// <summary>
    /// POST /customers.json — find-or-create a customer by reference. Composite: looks the reference up and
    /// creates the customer only if absent. Returns the provider customer id.
    /// </summary>
    [HttpPost("customers")]
    [ProducesResponseType(typeof(CustomerIdResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> FindOrCreateCustomer([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var customerId = await _billingClient.FindOrCreateCustomerAsync(
            request.Customer.Reference,
            request.Customer.Email,
            request.Customer.FirstName,
            request.Customer.LastName,
            cancellationToken);

        return Ok(new CustomerIdResponse(customerId));
    }

    /// <summary>GET /customers/lookup.json?reference={reference} — read-only lookup of the provider customer id.</summary>
    [HttpGet("customers/lookup")]
    [ProducesResponseType(typeof(CustomerIdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindCustomerId([FromQuery(Name = "reference")][Required] string reference, CancellationToken cancellationToken)
    {
        var customerId = await _billingClient.FindCustomerIdAsync(reference, cancellationToken);
        if (customerId is null)
        {
            return NotFound();
        }

        return Ok(new CustomerIdResponse(customerId));
    }

    /// <summary>POST /subscriptions.json — enrolls a customer in a plan.</summary>
    [HttpPost("subscriptions")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSubscription([FromBody] CreateSubscriptionRequest request, CancellationToken cancellationToken)
    {
        var providerCustomerId = request.Subscription.CustomerId!.Value.ToString(CultureInfo.InvariantCulture);
        var subscription = await _billingClient.CreateSubscriptionAsync(providerCustomerId, request.Subscription.ProductHandle, cancellationToken);
        return Created($"api/maxio/subscriptions/{subscription.SubscriptionId}", SubscriptionResponse.From(subscription));
    }

    /// <summary>GET /subscriptions/{subscription_id}.json — reads a subscription. The `include` query is inert.</summary>
    [HttpGet("subscriptions/{subscriptionId}")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReadSubscription(string subscriptionId, [FromQuery(Name = "include")] string? include, CancellationToken cancellationToken)
    {
        if (InvalidId(subscriptionId, "subscription_id", out var problem))
        {
            return problem;
        }

        var subscription = await _billingClient.ReadSubscriptionAsync(subscriptionId, cancellationToken);
        return Ok(SubscriptionResponse.From(subscription));
    }

    /// <summary>GET /customers/{customer_id}/subscriptions.json — lists a customer's subscriptions.</summary>
    [HttpGet("customers/{customerId}/subscriptions")]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCustomerSubscriptions(string customerId, CancellationToken cancellationToken)
    {
        if (InvalidId(customerId, "customer_id", out var problem))
        {
            return problem;
        }

        var subscriptions = await _billingClient.ListCustomerSubscriptionsAsync(customerId, cancellationToken);
        return Ok(subscriptions.Select(SubscriptionResponse.From).ToList());
    }

    /// <summary>
    /// GET /components/lookup.json?handle={handle} — verifies the CONFIGURED metered component exists, is
    /// metered, and belongs to the configured family. The `handle` query is inert (the configured handle is
    /// always used). 204 on success; a misconfiguration surfaces as 422 via the middleware.
    /// </summary>
    [HttpGet("metered-component/verify")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> VerifyMeteredComponent([FromQuery(Name = "handle")] string? handle, CancellationToken cancellationToken)
    {
        await _billingClient.VerifyMeteredComponentAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// POST /subscriptions/{subscription_id}/components/{component_id}/usages.json — records usage.
    /// The `component_id` path segment is inert (the configured metered component is always used).
    /// </summary>
    [HttpPost("subscriptions/{subscriptionId}/components/{componentId}/usages")]
    [ProducesResponseType(typeof(UsageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordUsage(string subscriptionId, string componentId, [FromBody] CreateUsageRequest request, CancellationToken cancellationToken)
    {
        if (InvalidId(subscriptionId, "subscription_id", out var problem))
        {
            return problem;
        }

        var usage = await _billingClient.RecordUsageAsync(subscriptionId, request.Usage.Quantity, request.Usage.Memo, cancellationToken);
        return Ok(usage);
    }

    /// <summary>
    /// GET /subscriptions/{subscription_id}/components/{component_id}.json — period-to-date usage summary.
    /// Composite (reads the subscription component + the subscription). The `component_id` path segment is inert.
    /// </summary>
    [HttpGet("subscriptions/{subscriptionId}/components/{componentId}/summary")]
    [ProducesResponseType(typeof(UsageSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsageSummary(string subscriptionId, string componentId, CancellationToken cancellationToken)
    {
        if (InvalidId(subscriptionId, "subscription_id", out var problem))
        {
            return problem;
        }

        var summary = await _billingClient.GetUsageSummaryAsync(subscriptionId, cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// POST /subscriptions/{subscription_id}/migrations/preview.json — quotes a plan change.
    /// `timing` (Immediate|AtRenewal) is a control field, not a Maxio parameter.
    /// </summary>
    [HttpPost("subscriptions/{subscriptionId}/migrations/preview")]
    [ProducesResponseType(typeof(PlanChangeQuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewPlanChange(string subscriptionId, [FromBody] MigrationRequest request, CancellationToken cancellationToken)
    {
        if (InvalidId(subscriptionId, "subscription_id", out var problem))
        {
            return problem;
        }

        if (!TryParsePlanChangeTiming(request.Timing, out var timing))
        {
            return ValidationProblem(ModelState);
        }

        var quote = await _billingClient.PreviewPlanChangeAsync(subscriptionId, request.Migration.ProductHandle, timing, cancellationToken);
        return Ok(PlanChangeQuoteResponse.From(quote));
    }

    /// <summary>
    /// POST /subscriptions/{subscription_id}/migrations.json — commits a plan change.
    /// `timing` selects the client's underlying operation: Immediate → migrate now; AtRenewal → update at renewal.
    /// </summary>
    [HttpPost("subscriptions/{subscriptionId}/migrations")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CommitPlanChange(string subscriptionId, [FromBody] MigrationRequest request, CancellationToken cancellationToken)
    {
        if (InvalidId(subscriptionId, "subscription_id", out var problem))
        {
            return problem;
        }

        if (!TryParsePlanChangeTiming(request.Timing, out var timing))
        {
            return ValidationProblem(ModelState);
        }

        var subscription = await _billingClient.CommitPlanChangeAsync(subscriptionId, request.Migration.ProductHandle, timing, cancellationToken);
        return Ok(SubscriptionResponse.From(subscription));
    }

    /// <summary>POST /subscriptions/{subscription_id}/hold.json — pauses a subscription. The body is inert.</summary>
    [HttpPost("subscriptions/{subscriptionId}/hold")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Pause(string subscriptionId, [FromBody] PauseRequest? request, CancellationToken cancellationToken)
    {
        if (InvalidId(subscriptionId, "subscription_id", out var problem))
        {
            return problem;
        }

        var subscription = await _billingClient.PauseAsync(subscriptionId, cancellationToken);
        return Ok(SubscriptionResponse.From(subscription));
    }

    /// <summary>
    /// POST /subscriptions/{subscription_id}/resume.json — resumes a paused subscription.
    /// The calendar-billing resumption-charge query is inert.
    /// </summary>
    [HttpPost("subscriptions/{subscriptionId}/resume")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resume(
        string subscriptionId,
        [FromQuery(Name = "calendar_billing[resumption_charge]")] string? resumptionCharge,
        CancellationToken cancellationToken)
    {
        if (InvalidId(subscriptionId, "subscription_id", out var problem))
        {
            return problem;
        }

        var subscription = await _billingClient.ResumeAsync(subscriptionId, cancellationToken);
        return Ok(SubscriptionResponse.From(subscription));
    }

    /// <summary>
    /// DELETE /subscriptions/{subscription_id}.json — cancels a subscription. `cancellation_message` maps to
    /// the reason; `timing` (Immediate|EndOfPeriod) is a control field. `reason_code` is inert.
    /// </summary>
    [HttpDelete("subscriptions/{subscriptionId}")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(string subscriptionId, [FromBody] CancellationRequest request, CancellationToken cancellationToken)
    {
        if (InvalidId(subscriptionId, "subscription_id", out var problem))
        {
            return problem;
        }

        if (!Enum.TryParse<CancelTiming>(request.Timing, ignoreCase: true, out var timing))
        {
            ModelState.AddModelError("timing", "Must be 'Immediate' or 'EndOfPeriod'.");
            return ValidationProblem(ModelState);
        }

        var subscription = await _billingClient.CancelAsync(subscriptionId, timing, request.Subscription.CancellationMessage, cancellationToken);
        return Ok(SubscriptionResponse.From(subscription));
    }

    /// <summary>PUT /subscriptions/{subscription_id}/reactivate.json — reactivates a canceled subscription. The body is inert.</summary>
    [HttpPut("subscriptions/{subscriptionId}/reactivate")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reactivate(string subscriptionId, [FromBody] ReactivateRequest? request, CancellationToken cancellationToken)
    {
        if (InvalidId(subscriptionId, "subscription_id", out var problem))
        {
            return problem;
        }

        var subscription = await _billingClient.ReactivateAsync(subscriptionId, cancellationToken);
        return Ok(SubscriptionResponse.From(subscription));
    }

    /// <summary>
    /// The client parses provider ids with <c>double.Parse</c>; a non-numeric id would otherwise throw an
    /// unguarded <c>FormatException</c> and leak as a raw 500. Validate up front and return a clean 400 instead.
    /// </summary>
    private bool InvalidId(string id, string name, out IActionResult problem)
    {
        if (double.TryParse(id, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            problem = null!;
            return false;
        }

        ModelState.AddModelError(name, $"'{name}' must be a numeric Maxio id.");
        problem = ValidationProblem(ModelState);
        return true;
    }

    private bool TryParsePlanChangeTiming(string value, out PlanChangeTiming timing)
    {
        if (Enum.TryParse(value, ignoreCase: true, out timing))
        {
            return true;
        }

        ModelState.AddModelError("timing", "Must be 'Immediate' or 'AtRenewal'.");
        return false;
    }
}
