using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.PublicApi.MaxioBilling;

/// <summary>
/// Exposes <see cref="IBillingClient"/> (the Maxio Advanced Billing client) as an HTTP microservice:
/// one endpoint per client method, one-to-one.
///
/// Contract policy (agreed up front):
///   * Each request DTO is shaped after the corresponding Maxio operation's full parameter set, so the
///     external contract matches Maxio's API regardless of the narrower shape the client accepts. The
///     controller forwards only the subset the client can consume; every non-forwarded field is
///     flagged "NOT FORWARDED" on the endpoint or its DTO.
///   * Operations whose required path params are fixed by server config - product_family_id (products,
///     metered component) and component_id (usage, balance, metered component) - omit those params.
///     The endpoints operate on the product family + metered component configured in MaxioSettings.
///
/// Auth: [AllowAnonymous] - this is an internal service-to-service microservice.
/// Errors: BillingProviderException / MeteredComponentMisconfiguredException propagate to the shared
/// ExceptionMiddleware, which maps them to status codes consistently with the rest of PublicApi.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/maxio")]
[Produces("application/json")]
public class MaxioBillingController : ControllerBase
{
    private readonly IBillingClient _billing;

    public MaxioBillingController(IBillingClient billing)
    {
        _billing = billing;
    }

    // ---- Products & components (configured product family) ------------------------------------

    /// <summary>listProductsForProductFamily - GET /product_families/{product_family_id}/products.json.</summary>
    /// <remarks>product_family_id comes from configuration; the `page`/`per_page` query params are accepted but NOT forwarded.</remarks>
    [HttpGet("products")]
    public async Task<IActionResult> ListProducts([FromQuery] ListProductsQuery query, CancellationToken cancellationToken)
        => Ok(await _billing.ListPlansAsync(cancellationToken));

    /// <summary>readComponent - GET /product_families/{product_family_id}/components/{component_id}.json.</summary>
    /// <remarks>Both path params come from configuration, so this endpoint takes no inputs; it reads the configured metered component.</remarks>
    [HttpGet("metered-component")]
    public async Task<IActionResult> GetMeteredComponent(CancellationToken cancellationToken)
        => Ok(await _billing.GetMeteredComponentAsync(cancellationToken));

    // ---- Customers ----------------------------------------------------------------------------

    /// <summary>Ensure a customer exists - readCustomerByReference (GET /customers/lookup.json) then, if absent, createCustomer (POST /customers.json).</summary>
    /// <remarks>Forwards customer.reference, email, first_name, last_name. All four are required here (reference is the idempotency key). Every other Create-Customer field is accepted but NOT forwarded.</remarks>
    [HttpPost("customers")]
    public async Task<IActionResult> EnsureCustomer([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var c = request.Customer;
        if (string.IsNullOrWhiteSpace(c.Reference)
            || string.IsNullOrWhiteSpace(c.Email)
            || string.IsNullOrWhiteSpace(c.FirstName)
            || string.IsNullOrWhiteSpace(c.LastName))
        {
            return BadRequest("customer.reference, customer.email, customer.first_name and customer.last_name are required.");
        }

        return Ok(await _billing.EnsureCustomerAsync(c.Reference!, c.Email!, c.FirstName!, c.LastName!, cancellationToken));
    }

    /// <summary>listCustomerSubscriptions - GET /customers/{customer_id}/subscriptions.json.</summary>
    [HttpGet("customers/{customerId:int}/subscriptions")]
    public async Task<IActionResult> ListCustomerSubscriptions(int customerId, CancellationToken cancellationToken)
        => Ok(await _billing.ListCustomerSubscriptionsAsync(customerId, cancellationToken));

    // ---- Subscriptions ------------------------------------------------------------------------

    /// <summary>createSubscription - POST /subscriptions.json.</summary>
    /// <remarks>Forwards subscription.customer_id, product_handle, and a chargify_token from payment_profile_attributes/credit_card_attributes.
    /// customer_id and product_handle are required here (the client does not accept customer_reference/customer_attributes or product_id).
    /// payment_collection_method is NOT forwarded - the client derives it. All other fields are accepted but NOT forwarded.</remarks>
    [HttpPost("subscriptions")]
    public async Task<IActionResult> CreateSubscription([FromBody] CreateSubscriptionRequest request, CancellationToken cancellationToken)
    {
        var s = request.Subscription;
        if (s.CustomerId is null || string.IsNullOrWhiteSpace(s.ProductHandle))
        {
            return BadRequest("subscription.customer_id and subscription.product_handle are required by this microservice.");
        }

        var paymentToken = s.PaymentProfileAttributes?.ChargifyToken ?? s.CreditCardAttributes?.ChargifyToken;
        return Ok(await _billing.CreateSubscriptionAsync(s.CustomerId.Value, s.ProductHandle!, paymentToken, cancellationToken));
    }

    /// <summary>readSubscription - GET /subscriptions/{subscription_id}.json.</summary>
    /// <remarks>The `include[]` query param is accepted but NOT forwarded.</remarks>
    [HttpGet("subscriptions/{subscriptionId:int}")]
    public async Task<IActionResult> GetSubscription(int subscriptionId, [FromQuery] GetSubscriptionQuery query, CancellationToken cancellationToken)
        => Ok(await _billing.GetSubscriptionAsync(subscriptionId, cancellationToken));

    /// <summary>createUsage - POST /subscriptions/{subscription_id_or_reference}/components/{component_id}/usages.json.</summary>
    /// <remarks>component_id comes from configuration (the metered component). Forwards usage.quantity (required) and usage.memo.
    /// price_point_id, billing_schedule and custom_price are accepted but NOT forwarded. The spec allows a reference in the path,
    /// but the client accepts a numeric subscription id only.</remarks>
    [HttpPost("subscriptions/{subscriptionIdOrReference:int}/usages")]
    public async Task<IActionResult> RecordUsage(int subscriptionIdOrReference, [FromBody] CreateUsageRequest request, CancellationToken cancellationToken)
    {
        if (request.Usage.Quantity is null)
        {
            return BadRequest("usage.quantity is required.");
        }

        return Ok(await _billing.RecordUsageAsync(subscriptionIdOrReference, request.Usage.Quantity.Value, request.Usage.Memo, cancellationToken));
    }

    /// <summary>readSubscriptionComponent - GET /subscriptions/{subscription_id}/components/{component_id}.json.</summary>
    /// <remarks>component_id comes from configuration; returns the configured metered component's period-to-date unit balance.</remarks>
    [HttpGet("subscriptions/{subscriptionId:int}/component-balance")]
    public async Task<IActionResult> GetUsageBalance(int subscriptionId, CancellationToken cancellationToken)
        => Ok(await _billing.GetUsageBalanceAsync(subscriptionId, cancellationToken));

    /// <summary>previewSubscriptionProductMigration - POST /subscriptions/{subscription_id}/migrations/preview.json.</summary>
    /// <remarks>Forwards migration.product_handle (required). preserve_period is NOT forwarded (always true); product_id, price points,
    /// include_* flags, proration and proration_date are accepted but NOT forwarded.</remarks>
    [HttpPost("subscriptions/{subscriptionId:int}/migrations/preview")]
    public async Task<IActionResult> PreviewPlanChange(int subscriptionId, [FromBody] MigrationPreviewRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Migration.ProductHandle))
        {
            return BadRequest("migration.product_handle is required by this microservice.");
        }

        return Ok(await _billing.PreviewPlanChangeNowAsync(subscriptionId, request.Migration.ProductHandle!, cancellationToken));
    }

    /// <summary>migrateSubscriptionProduct - POST /subscriptions/{subscription_id}/migrations.json (immediate, prorated).</summary>
    /// <remarks>Forwards migration.product_handle (required). preserve_period is NOT forwarded (always true); the rest of the migration body is accepted but NOT forwarded.</remarks>
    [HttpPost("subscriptions/{subscriptionId:int}/migrations")]
    public async Task<IActionResult> CommitPlanChange(int subscriptionId, [FromBody] MigrationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Migration.ProductHandle))
        {
            return BadRequest("migration.product_handle is required by this microservice.");
        }

        return Ok(await _billing.CommitPlanChangeNowAsync(subscriptionId, request.Migration.ProductHandle!, cancellationToken));
    }

    /// <summary>updateSubscription - PUT /subscriptions/{subscription_id}.json, used here to schedule a plan change at renewal.</summary>
    /// <remarks>The client maps this general update to a delayed product change: it forwards subscription.product_handle (required) and always
    /// sets product_change_delayed=true. Every other Update-Subscription field (including product_change_delayed itself) is accepted but NOT forwarded.</remarks>
    [HttpPut("subscriptions/{subscriptionId:int}")]
    public async Task<IActionResult> SchedulePlanChangeAtRenewal(int subscriptionId, [FromBody] UpdateSubscriptionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Subscription.ProductHandle))
        {
            return BadRequest("subscription.product_handle is required by this microservice.");
        }

        return Ok(await _billing.SchedulePlanChangeAtRenewalAsync(subscriptionId, request.Subscription.ProductHandle!, cancellationToken));
    }

    /// <summary>pauseSubscription - POST /subscriptions/{subscription_id}/hold.json.</summary>
    /// <remarks>Body is optional; hold.automatically_resume_at is accepted but NOT forwarded (the client always issues an indefinite hold).</remarks>
    [HttpPost("subscriptions/{subscriptionId:int}/hold")]
    public async Task<IActionResult> PauseSubscription(int subscriptionId, [FromBody] PauseRequest? request, CancellationToken cancellationToken)
        => Ok(await _billing.PauseSubscriptionAsync(subscriptionId, cancellationToken));

    /// <summary>resumeSubscription - POST /subscriptions/{subscription_id}/resume.json.</summary>
    /// <remarks>The resumption-charge query param is accepted but NOT forwarded.</remarks>
    [HttpPost("subscriptions/{subscriptionId:int}/resume")]
    public async Task<IActionResult> ResumeSubscription(int subscriptionId, [FromQuery] ResumeSubscriptionQuery query, CancellationToken cancellationToken)
        => Ok(await _billing.ResumeSubscriptionAsync(subscriptionId, cancellationToken));

    /// <summary>cancelSubscription - DELETE /subscriptions/{subscription_id}.json (immediate).</summary>
    /// <remarks>Body is optional; forwards subscription.cancellation_message as the reason. reason_code is accepted but NOT forwarded.</remarks>
    [HttpDelete("subscriptions/{subscriptionId:int}")]
    public async Task<IActionResult> CancelSubscriptionImmediately(int subscriptionId, [FromBody] CancellationRequest? request, CancellationToken cancellationToken)
        => Ok(await _billing.CancelSubscriptionImmediatelyAsync(subscriptionId, request?.Subscription.CancellationMessage, cancellationToken));

    /// <summary>initiateDelayedCancellation - POST /subscriptions/{subscription_id}/delayed_cancel.json (cancel at end of period).</summary>
    /// <remarks>Body is optional; forwards subscription.cancellation_message as the reason. reason_code is accepted but NOT forwarded.</remarks>
    [HttpPost("subscriptions/{subscriptionId:int}/delayed_cancel")]
    public async Task<IActionResult> ScheduleCancelAtEndOfPeriod(int subscriptionId, [FromBody] CancellationRequest? request, CancellationToken cancellationToken)
        => Ok(await _billing.ScheduleCancelAtEndOfPeriodAsync(subscriptionId, request?.Subscription.CancellationMessage, cancellationToken));

    /// <summary>reactivateSubscription - PUT /subscriptions/{subscription_id}/reactivate.json.</summary>
    /// <remarks>Body is optional and, if present, entirely NOT forwarded (the client reactivates with no options).</remarks>
    [HttpPut("subscriptions/{subscriptionId:int}/reactivate")]
    public async Task<IActionResult> ReactivateSubscription(int subscriptionId, [FromBody] ReactivateSubscriptionRequest? request, CancellationToken cancellationToken)
        => Ok(await _billing.ReactivateSubscriptionAsync(subscriptionId, cancellationToken));
}
