using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.MaxioBillingTestApi.Controllers;

// Exposes the existing MaxioBillingClient (via the IBillingClient seam) as a Maxio
// billing microservice — one HTTP endpoint per client method, routes taken verbatim
// from docs/maxio-billing-service-route-map.md. Each action does exactly three
// things: bind + forward the request to its one client method, return the client's
// typed result untouched on success, and on failure map the client's exception to a
// named error category (HTTP status + descriptive message). No request-side
// validation/coercion/resolution and no success-response reshaping happen here.
[ApiController]
[Route("api/maxio")]
public class MaxioBillingController : ControllerBase
{
    private readonly IBillingClient _billing;

    public MaxioBillingController(IBillingClient billing)
    {
        _billing = billing;
    }

    // GET /product_families/{product_family_id}/products.json  ->  ListPlansAsync
    [HttpGet("product-families/{productFamilyId}/products")]
    public async Task<IActionResult> ListPlans(string productFamilyId, CancellationToken ct)
    {
        try
        {
            var plans = await _billing.ListPlansAsync(ct);
            return Ok(plans);
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    // GET /customers/lookup.json  ->  FindCustomerIdAsync
    [HttpGet("customers/lookup")]
    public async Task<IActionResult> LookupCustomer([FromQuery] string reference, CancellationToken ct)
    {
        try
        {
            var id = await _billing.FindCustomerIdAsync(reference, ct);
            return id is null ? NotFound(NotFoundBody("customer")) : Ok(id);
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    // GET /customers/lookup.json -> POST /customers.json  ->  EnsureCustomerAsync
    [HttpPost("customers")]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequestDto request, CancellationToken ct)
    {
        var c = request.Customer ?? new CustomerBodyDto();
        try
        {
            var id = await _billing.EnsureCustomerAsync(
                c.Reference ?? string.Empty, c.Email ?? string.Empty,
                c.FirstName ?? string.Empty, c.LastName ?? string.Empty, ct);
            return Ok(id);
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    // GET /customers/{customer_id}/subscriptions.json  ->  ListCustomerSubscriptionsAsync
    [HttpGet("customers/{customerId:int}/subscriptions")]
    public async Task<IActionResult> ListCustomerSubscriptions(int customerId, CancellationToken ct)
    {
        try
        {
            var subs = await _billing.ListCustomerSubscriptionsAsync(customerId, ct);
            return Ok(subs);
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    // POST /subscriptions.json  ->  SubscribeAsync
    [HttpPost("subscriptions")]
    public async Task<IActionResult> Subscribe([FromBody] CreateSubscriptionRequestDto request, CancellationToken ct)
    {
        var s = request.Subscription ?? new CreateSubscriptionBodyDto();
        try
        {
            var sub = await _billing.SubscribeAsync(s.CustomerId, s.ProductHandle ?? string.Empty, ct);
            return Ok(sub);
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    // GET /subscriptions/{subscription_id}.json  ->  GetSubscriptionAsync
    [HttpGet("subscriptions/{subscriptionId:int}")]
    public async Task<IActionResult> GetSubscription(int subscriptionId, CancellationToken ct)
    {
        try
        {
            var sub = await _billing.GetSubscriptionAsync(subscriptionId, ct);
            return sub is null ? NotFound(NotFoundBody("subscription")) : Ok(sub);
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    // POST /subscriptions/{subscription_id}/migrations/preview.json  ->  PreviewPlanChangeAsync (apply now)
    [HttpPost("subscriptions/{subscriptionId:int}/migrations/preview")]
    public async Task<IActionResult> PreviewMigration(int subscriptionId,
        [FromBody] MigrationRequestDto request, CancellationToken ct)
    {
        var m = request.Migration ?? new MigrationBodyDto();
        try
        {
            var preview = await _billing.PreviewPlanChangeAsync(
                subscriptionId, string.Empty, m.ProductHandle ?? string.Empty, applyNow: true, ct);
            return Ok(preview);
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    // POST /subscriptions/{subscription_id}/migrations.json  ->  ChangePlanAsync (apply now)
    [HttpPost("subscriptions/{subscriptionId:int}/migrations")]
    public async Task<IActionResult> Migrate(int subscriptionId,
        [FromBody] MigrationRequestDto request, CancellationToken ct)
    {
        var m = request.Migration ?? new MigrationBodyDto();
        try
        {
            var sub = await _billing.ChangePlanAsync(
                subscriptionId, m.ProductHandle ?? string.Empty, applyNow: true, ct);
            return Ok(sub);
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    // POST /subscriptions/{subscription_id}/hold.json  ->  PauseAsync
    [HttpPost("subscriptions/{subscriptionId:int}/hold")]
    public async Task<IActionResult> Hold(int subscriptionId, CancellationToken ct)
    {
        try
        {
            var sub = await _billing.PauseAsync(subscriptionId, ct);
            return Ok(sub);
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    // POST /subscriptions/{subscription_id}/resume.json  ->  ResumeAsync
    [HttpPost("subscriptions/{subscriptionId:int}/resume")]
    public async Task<IActionResult> Resume(int subscriptionId, CancellationToken ct)
    {
        try
        {
            var sub = await _billing.ResumeAsync(subscriptionId, ct);
            return Ok(sub);
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    // DELETE /subscriptions/{subscription_id}.json  ->  CancelAsync (immediate)
    [HttpDelete("subscriptions/{subscriptionId:int}")]
    public async Task<IActionResult> Cancel(int subscriptionId,
        [FromBody] CancellationRequestDto? request, CancellationToken ct)
    {
        var reason = request?.Subscription?.CancellationMessage;
        try
        {
            var sub = await _billing.CancelAsync(subscriptionId, endOfPeriod: false, reason, ct);
            return Ok(sub);
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    // PUT /subscriptions/{subscription_id}/reactivate.json  ->  ReactivateAsync
    [HttpPut("subscriptions/{subscriptionId:int}/reactivate")]
    public async Task<IActionResult> Reactivate(int subscriptionId, CancellationToken ct)
    {
        try
        {
            var sub = await _billing.ReactivateAsync(subscriptionId, ct);
            return Ok(sub);
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    // GET /components/lookup.json?handle={handle}  ->  EnsureMeteredComponentAsync
    [HttpGet("metered-component/verify")]
    public async Task<IActionResult> VerifyMeteredComponent(CancellationToken ct)
    {
        try
        {
            await _billing.EnsureMeteredComponentAsync(ct);
            return Ok();
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    // POST /subscriptions/{subscription_id}/components/{component_id}/usages.json  ->  RecordUsageAsync
    [HttpPost("subscriptions/{subscriptionId:int}/components/{componentId}/usages")]
    public async Task<IActionResult> RecordUsage(int subscriptionId, string componentId,
        [FromBody] CreateUsageRequestDto request, CancellationToken ct)
    {
        var u = request.Usage ?? new UsageBodyDto();
        try
        {
            var recorded = await _billing.RecordUsageAsync(subscriptionId, u.Quantity, u.Memo, ct);
            return Ok(recorded);
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    // GET /subscriptions/{id}/components/{comp}.json + GET /subscriptions/{id}.json
    //   ->  GetPeriodToDateUsageAsync (combined period-to-date read)
    [HttpGet("subscriptions/{subscriptionId:int}/components/{componentId}/summary")]
    public async Task<IActionResult> ComponentSummary(int subscriptionId, string componentId, CancellationToken ct)
    {
        try
        {
            var total = await _billing.GetPeriodToDateUsageAsync(subscriptionId, ct);
            return Ok(total);
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    // ----- error categorization ---------------------------------------------
    // Maps the client's exception types to a named error category (HTTP status +
    // descriptive message). Keyed to the class of failure, never to a specific
    // input; categories stay consistent across every endpoint.

    private IActionResult MapError(Exception ex) => ex switch
    {
        BillingConfigurationException => Problem(422, "billing_configuration", ex.Message),
        BillingProviderException => Problem(502, "billing_provider_error", ex.Message),
        OperationCanceledException => Problem(499, "request_canceled", "The request was canceled."),
        _ => Problem(500, "unexpected_error", ex.Message)
    };

    private IActionResult Problem(int status, string category, string message)
    {
        return new ObjectResult(new ErrorResponse { Category = category, Message = message })
        {
            StatusCode = status
        };
    }

    private static ErrorResponse NotFoundBody(string resource) =>
        new() { Category = "not_found", Message = $"The requested {resource} does not exist." };
}

public class ErrorResponse
{
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
