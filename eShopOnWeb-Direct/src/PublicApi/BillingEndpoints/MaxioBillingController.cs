using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using BlazorShared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace Microsoft.eShopWeb.PublicApi.BillingEndpoints;

/// <summary>
/// Thin microservice surface over <see cref="IBillingClient"/> (the Maxio-over-raw-HttpClient
/// implementation). Every action maps 1:1 to one client method, which in turn maps 1:1 to one Maxio
/// Advanced Billing REST operation - the controller adds routing, input validation and OpenAPI shape
/// only, no business logic.
///
/// <para>Error handling is delegated to the global <c>ExceptionMiddleware</c>: the client throws
/// <see cref="Microsoft.eShopWeb.ApplicationCore.Exceptions.BillingProviderException"/> (mapped to 422
/// for a provider 4xx, 502 for a provider 5xx/timeout) and
/// <see cref="Microsoft.eShopWeb.ApplicationCore.Exceptions.MeteredComponentMisconfiguredException"/>
/// (mapped to 500), both carrying an already-curated, caller-safe message. Malformed input is rejected
/// with 400 by the <see cref="ApiControllerAttribute"/> automatic model-state validation before the
/// client is ever called.</para>
///
/// <para><see cref="AllowAnonymousAttribute"/> is deliberate: this is an internal service-to-service
/// surface, consistent with the harness (the mock ignores auth).</para>
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/billing")]
[Produces("application/json")]
[ProducesResponseType(typeof(ErrorDetails), StatusCodes.Status422UnprocessableEntity)]
[ProducesResponseType(typeof(ErrorDetails), StatusCodes.Status502BadGateway)]
[ProducesResponseType(typeof(ErrorDetails), StatusCodes.Status500InternalServerError)]
public class MaxioBillingController : ControllerBase
{
    private readonly IBillingClient _billingClient;

    public MaxioBillingController(IBillingClient billingClient)
    {
        _billingClient = billingClient;
    }

    // ---- Catalog -----------------------------------------------------------------------------

    /// <summary>GET product_families/{family}/products.json (listProductsForProductFamily).</summary>
    [HttpGet("plans")]
    [ProducesResponseType(typeof(IReadOnlyList<BillingPlan>), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "List available plans",
        Description = "Returns the product family's available recurring plans.",
        OperationId = "billing.listPlans",
        Tags = new[] { "MaxioBilling" })]
    public async Task<ActionResult<IReadOnlyList<BillingPlan>>> ListPlans(CancellationToken cancellationToken)
    {
        var plans = await _billingClient.ListPlansAsync(cancellationToken);
        return Ok(plans);
    }

    /// <summary>GET product_families/{family}/components/{component}.json (readComponent).</summary>
    [HttpGet("metered-component")]
    [ProducesResponseType(typeof(BillingComponentInfo), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "Read the configured metered component",
        Description = "Resolves and validates the configured metered usage component on the product family.",
        OperationId = "billing.getMeteredComponent",
        Tags = new[] { "MaxioBilling" })]
    public async Task<ActionResult<BillingComponentInfo>> GetMeteredComponent(CancellationToken cancellationToken)
    {
        var component = await _billingClient.GetMeteredComponentAsync(cancellationToken);
        return Ok(component);
    }

    // ---- Customers ---------------------------------------------------------------------------

    /// <summary>GET customers/lookup.json then POST customers.json (idempotent on reference).</summary>
    [HttpPost("customers")]
    [ProducesResponseType(typeof(CustomerResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [SwaggerOperation(
        Summary = "Ensure a customer exists",
        Description = "Looks up the customer by reference and creates one if absent; returns the provider customer id.",
        OperationId = "billing.ensureCustomer",
        Tags = new[] { "MaxioBilling" })]
    public async Task<ActionResult<CustomerResult>> EnsureCustomer([FromBody] EnsureCustomerRequest request, CancellationToken cancellationToken)
    {
        var customerId = await _billingClient.EnsureCustomerAsync(
            request.Reference, request.Email, request.FirstName, request.LastName, cancellationToken);
        return Ok(new CustomerResult(customerId));
    }

    /// <summary>GET customers/{customer_id}/subscriptions.json (listCustomerSubscriptions).</summary>
    [HttpGet("customers/{customerId:int}/subscriptions")]
    [ProducesResponseType(typeof(IReadOnlyList<BillingSubscription>), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "List a customer's subscriptions",
        Description = "Returns every subscription belonging to the given provider customer id.",
        OperationId = "billing.listCustomerSubscriptions",
        Tags = new[] { "MaxioBilling" })]
    public async Task<ActionResult<IReadOnlyList<BillingSubscription>>> ListCustomerSubscriptions(int customerId, CancellationToken cancellationToken)
    {
        var subscriptions = await _billingClient.ListCustomerSubscriptionsAsync(customerId, cancellationToken);
        return Ok(subscriptions);
    }

    // ---- Subscriptions -----------------------------------------------------------------------

    /// <summary>POST subscriptions.json (createSubscription).</summary>
    [HttpPost("subscriptions")]
    [ProducesResponseType(typeof(BillingSubscription), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [SwaggerOperation(
        Summary = "Create a subscription",
        Description = "Subscribes a provider customer to a product; omit paymentToken for remittance collection.",
        OperationId = "billing.createSubscription",
        Tags = new[] { "MaxioBilling" })]
    public async Task<ActionResult<BillingSubscription>> CreateSubscription([FromBody] CreateSubscriptionRequest request, CancellationToken cancellationToken)
    {
        var subscription = await _billingClient.CreateSubscriptionAsync(
            request.CustomerId, request.ProductHandle, request.PaymentToken, cancellationToken);
        return CreatedAtAction(nameof(GetSubscription), new { subscriptionId = subscription.ProviderSubscriptionId }, subscription);
    }

    /// <summary>GET subscriptions/{subscription_id}.json (readSubscription).</summary>
    [HttpGet("subscriptions/{subscriptionId:int}")]
    [ProducesResponseType(typeof(BillingSubscription), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "Read a subscription",
        Description = "Returns the current billing state of a single subscription.",
        OperationId = "billing.getSubscription",
        Tags = new[] { "MaxioBilling" })]
    public async Task<ActionResult<BillingSubscription>> GetSubscription(int subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await _billingClient.GetSubscriptionAsync(subscriptionId, cancellationToken);
        return Ok(subscription);
    }

    // ---- Usage -------------------------------------------------------------------------------

    /// <summary>POST subscriptions/{subscription_id}/components/{component}/usages.json (createUsage).</summary>
    [HttpPost("subscriptions/{subscriptionId:int}/usages")]
    [ProducesResponseType(typeof(BillingUsageResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [SwaggerOperation(
        Summary = "Record metered usage",
        Description = "Records a quantity of usage against the subscription's configured metered component.",
        OperationId = "billing.recordUsage",
        Tags = new[] { "MaxioBilling" })]
    public async Task<ActionResult<BillingUsageResult>> RecordUsage(int subscriptionId, [FromBody] RecordUsageRequest request, CancellationToken cancellationToken)
    {
        var usage = await _billingClient.RecordUsageAsync(subscriptionId, request.Quantity, request.Memo, cancellationToken);
        return Ok(usage);
    }

    /// <summary>GET subscriptions/{subscription_id}/components/{component}.json (readSubscriptionComponent).</summary>
    [HttpGet("subscriptions/{subscriptionId:int}/usage-balance")]
    [ProducesResponseType(typeof(UsageBalanceResult), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "Read period-to-date usage balance",
        Description = "Returns the current period's unit balance for the subscription's metered component.",
        OperationId = "billing.getUsageBalance",
        Tags = new[] { "MaxioBilling" })]
    public async Task<ActionResult<UsageBalanceResult>> GetUsageBalance(int subscriptionId, CancellationToken cancellationToken)
    {
        var balance = await _billingClient.GetUsageBalanceAsync(subscriptionId, cancellationToken);
        return Ok(new UsageBalanceResult(balance));
    }

    // ---- Plan changes ------------------------------------------------------------------------

    /// <summary>POST subscriptions/{subscription_id}/migrations/preview.json (previewSubscriptionProductMigration).</summary>
    [HttpPost("subscriptions/{subscriptionId:int}/plan-change/preview")]
    [ProducesResponseType(typeof(BillingProrationPreview), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [SwaggerOperation(
        Summary = "Preview an immediate plan change",
        Description = "Computes the prorated charge/credit of moving the subscription to the target product now.",
        OperationId = "billing.previewPlanChangeNow",
        Tags = new[] { "MaxioBilling" })]
    public async Task<ActionResult<BillingProrationPreview>> PreviewPlanChangeNow(int subscriptionId, [FromBody] PlanChangeRequest request, CancellationToken cancellationToken)
    {
        var preview = await _billingClient.PreviewPlanChangeNowAsync(subscriptionId, request.TargetProductHandle, cancellationToken);
        return Ok(preview);
    }

    /// <summary>POST subscriptions/{subscription_id}/migrations.json (migrateSubscriptionProduct, preserve_period=true).</summary>
    [HttpPost("subscriptions/{subscriptionId:int}/plan-change/now")]
    [ProducesResponseType(typeof(BillingSubscription), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [SwaggerOperation(
        Summary = "Commit an immediate plan change",
        Description = "Moves the subscription to the target product now, applying the prorated charge/credit.",
        OperationId = "billing.commitPlanChangeNow",
        Tags = new[] { "MaxioBilling" })]
    public async Task<ActionResult<BillingSubscription>> CommitPlanChangeNow(int subscriptionId, [FromBody] PlanChangeRequest request, CancellationToken cancellationToken)
    {
        var subscription = await _billingClient.CommitPlanChangeNowAsync(subscriptionId, request.TargetProductHandle, cancellationToken);
        return Ok(subscription);
    }

    /// <summary>PUT subscriptions/{subscription_id}.json with product_change_delayed=true (updateSubscription).</summary>
    [HttpPost("subscriptions/{subscriptionId:int}/plan-change/at-renewal")]
    [ProducesResponseType(typeof(BillingSubscription), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [SwaggerOperation(
        Summary = "Schedule a plan change at renewal",
        Description = "Schedules the subscription to move to the target product at the next renewal, with no proration.",
        OperationId = "billing.schedulePlanChangeAtRenewal",
        Tags = new[] { "MaxioBilling" })]
    public async Task<ActionResult<BillingSubscription>> SchedulePlanChangeAtRenewal(int subscriptionId, [FromBody] PlanChangeRequest request, CancellationToken cancellationToken)
    {
        var subscription = await _billingClient.SchedulePlanChangeAtRenewalAsync(subscriptionId, request.TargetProductHandle, cancellationToken);
        return Ok(subscription);
    }

    // ---- Lifecycle ---------------------------------------------------------------------------

    /// <summary>POST subscriptions/{subscription_id}/hold.json (pauseSubscription).</summary>
    [HttpPost("subscriptions/{subscriptionId:int}/pause")]
    [ProducesResponseType(typeof(BillingSubscription), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "Pause a subscription",
        Description = "Places the subscription on hold.",
        OperationId = "billing.pauseSubscription",
        Tags = new[] { "MaxioBilling" })]
    public async Task<ActionResult<BillingSubscription>> PauseSubscription(int subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await _billingClient.PauseSubscriptionAsync(subscriptionId, cancellationToken);
        return Ok(subscription);
    }

    /// <summary>POST subscriptions/{subscription_id}/resume.json (resumeSubscription).</summary>
    [HttpPost("subscriptions/{subscriptionId:int}/resume")]
    [ProducesResponseType(typeof(BillingSubscription), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "Resume a subscription",
        Description = "Resumes a paused (on-hold) subscription.",
        OperationId = "billing.resumeSubscription",
        Tags = new[] { "MaxioBilling" })]
    public async Task<ActionResult<BillingSubscription>> ResumeSubscription(int subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await _billingClient.ResumeSubscriptionAsync(subscriptionId, cancellationToken);
        return Ok(subscription);
    }

    /// <summary>DELETE subscriptions/{subscription_id}.json (cancelSubscription).</summary>
    [HttpPost("subscriptions/{subscriptionId:int}/cancel")]
    [ProducesResponseType(typeof(BillingSubscription), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "Cancel a subscription immediately",
        Description = "Cancels the subscription right away; an optional reason is recorded on the cancellation.",
        OperationId = "billing.cancelSubscriptionImmediately",
        Tags = new[] { "MaxioBilling" })]
    public async Task<ActionResult<BillingSubscription>> CancelSubscriptionImmediately(int subscriptionId, [FromBody] CancelSubscriptionRequest? request, CancellationToken cancellationToken)
    {
        var subscription = await _billingClient.CancelSubscriptionImmediatelyAsync(subscriptionId, request?.Reason, cancellationToken);
        return Ok(subscription);
    }

    /// <summary>POST subscriptions/{subscription_id}/delayed_cancel.json (initiateDelayedCancellation).</summary>
    [HttpPost("subscriptions/{subscriptionId:int}/cancel-at-period-end")]
    [ProducesResponseType(typeof(BillingSubscription), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "Schedule a cancellation at period end",
        Description = "Schedules the subscription to cancel at the end of the current period; returns its refreshed state.",
        OperationId = "billing.scheduleCancelAtEndOfPeriod",
        Tags = new[] { "MaxioBilling" })]
    public async Task<ActionResult<BillingSubscription>> ScheduleCancelAtEndOfPeriod(int subscriptionId, [FromBody] CancelSubscriptionRequest? request, CancellationToken cancellationToken)
    {
        var subscription = await _billingClient.ScheduleCancelAtEndOfPeriodAsync(subscriptionId, request?.Reason, cancellationToken);
        return Ok(subscription);
    }

    /// <summary>PUT subscriptions/{subscription_id}/reactivate.json (reactivateSubscription).</summary>
    [HttpPost("subscriptions/{subscriptionId:int}/reactivate")]
    [ProducesResponseType(typeof(BillingSubscription), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "Reactivate a subscription",
        Description = "Reactivates a canceled subscription.",
        OperationId = "billing.reactivateSubscription",
        Tags = new[] { "MaxioBilling" })]
    public async Task<ActionResult<BillingSubscription>> ReactivateSubscription(int subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await _billingClient.ReactivateSubscriptionAsync(subscriptionId, cancellationToken);
        return Ok(subscription);
    }
}

// ---- Request bodies ------------------------------------------------------------------------------

/// <summary>Body for <c>POST api/billing/customers</c>.</summary>
public class EnsureCustomerRequest
{
    [Required]
    [StringLength(255)]
    public string Reference { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;
}

/// <summary>Body for <c>POST api/billing/subscriptions</c>.</summary>
public class CreateSubscriptionRequest
{
    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }

    [Required]
    [StringLength(255)]
    public string ProductHandle { get; set; } = string.Empty;

    /// <summary>Optional Chargify.js payment token; when null the subscription is created with remittance collection.</summary>
    public string? PaymentToken { get; set; }
}

/// <summary>Body for <c>POST api/billing/subscriptions/{subscriptionId}/usages</c>.</summary>
public class RecordUsageRequest
{
    [Range(typeof(decimal), "-1000000", "1000000")]
    public decimal Quantity { get; set; }

    [StringLength(500)]
    public string? Memo { get; set; }
}

/// <summary>Body for the three <c>plan-change</c> endpoints.</summary>
public class PlanChangeRequest
{
    [Required]
    [StringLength(255)]
    public string TargetProductHandle { get; set; } = string.Empty;
}

/// <summary>Optional body for the two cancel endpoints.</summary>
public class CancelSubscriptionRequest
{
    [StringLength(500)]
    public string? Reason { get; set; }
}

// ---- Scalar result wrappers ----------------------------------------------------------------------
// The client methods below return a bare scalar; these give it a self-describing JSON object shape.

/// <summary>Result of <c>POST api/billing/customers</c>.</summary>
public record CustomerResult(int ProviderCustomerId);

/// <summary>Result of <c>GET api/billing/subscriptions/{subscriptionId}/usage-balance</c>.</summary>
public record UsageBalanceResult(int UnitBalance);
