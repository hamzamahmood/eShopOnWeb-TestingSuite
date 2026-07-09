using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.MaxioBillingTestApi.Models;

namespace Microsoft.eShopWeb.MaxioBillingTestApi.Controllers;

/// <summary>
/// Exposes the real <see cref="IBillingClient"/> (<c>MaxioBillingClient</c>) over HTTP, one endpoint
/// per client method, with routes taken verbatim from the route map. Every action does exactly three
/// things: bind + forward the request to its client method, return the client's typed result untouched
/// on success, and on failure map the client's <see cref="BillingProviderException"/> to a named error
/// category (an HTTP status + descriptive message).
///
/// There is deliberately NO shared/centralized error mapper: each action maps the failure modes of the
/// one client method it fronts inside its own catch block. The category vocabulary (the string names and
/// the status each maps to) is kept consistent across endpoints, and every arm is keyed to a CLASS of
/// provider failure — never to a specific test's input. Only the "absent"/not-found category differs
/// per endpoint, to name the resource that method reads/acts on.
/// </summary>
[ApiController]
[Route("api/maxio")]
public sealed class MaxioBillingController : ControllerBase
{
    private readonly IBillingClient _billing;

    public MaxioBillingController(IBillingClient billing)
    {
        _billing = billing;
    }

    // ---- Plans (UC1 step 1) ------------------------------------------------
    // GET /product_families/{product_family_id}/products.json
    // The client fixes the product family from configuration, so {productFamilyId} is an ignored path param.
    [HttpGet("product-families/{productFamilyId}/products")]
    public async Task<IActionResult> ListPlans(int productFamilyId, CancellationToken ct)
    {
        try
        {
            return Ok(await _billing.ListPlansAsync(ct));
        }
        catch (BillingProviderException ex)
        {
            return ex.StatusCode switch
            {
                null => ex.InnerException switch
                {
                    HttpRequestException => StatusCode(503, new { category = "provider-unavailable", message = ex.Message }),
                    TaskCanceledException => StatusCode(504, new { category = "provider-timeout", message = ex.Message }),
                    JsonException => StatusCode(502, new { category = "provider-error", message = ex.Message }),
                    _ => NotFound(new { category = "product-family-not-found", message = ex.Message })
                },
                400 => BadRequest(new { category = "invalid-request", message = ex.Message }),
                401 or 403 => StatusCode(502, new { category = "provider-authorization-failed", message = ex.Message }),
                404 => NotFound(new { category = "product-family-not-found", message = ex.Message }),
                422 => UnprocessableEntity(new { category = "billing-rule-violation", message = ex.Message }),
                429 => StatusCode(429, new { category = "provider-rate-limited", message = ex.Message }),
                _ => StatusCode(502, new { category = "provider-error", message = ex.Message })
            };
        }
    }

    // ---- Customers ---------------------------------------------------------
    // GET /customers/lookup.json -> POST /customers.json
    [HttpPost("customers")]
    public async Task<IActionResult> EnsureCustomer([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var c = request?.Customer;
        try
        {
            var id = await _billing.EnsureCustomerAsync(c?.Reference ?? string.Empty, c?.Email ?? string.Empty, c?.FirstName, c?.LastName, ct);
            return Ok(id);
        }
        catch (BillingProviderException ex)
        {
            return ex.StatusCode switch
            {
                null => ex.InnerException switch
                {
                    HttpRequestException => StatusCode(503, new { category = "provider-unavailable", message = ex.Message }),
                    TaskCanceledException => StatusCode(504, new { category = "provider-timeout", message = ex.Message }),
                    JsonException => StatusCode(502, new { category = "provider-error", message = ex.Message }),
                    _ => NotFound(new { category = "customer-not-found", message = ex.Message })
                },
                400 => BadRequest(new { category = "invalid-request", message = ex.Message }),
                401 or 403 => StatusCode(502, new { category = "provider-authorization-failed", message = ex.Message }),
                404 => NotFound(new { category = "customer-not-found", message = ex.Message }),
                422 => UnprocessableEntity(new { category = "billing-rule-violation", message = ex.Message }),
                429 => StatusCode(429, new { category = "provider-rate-limited", message = ex.Message }),
                _ => StatusCode(502, new { category = "provider-error", message = ex.Message })
            };
        }
    }

    // GET /customers/lookup.json?reference=
    [HttpGet("customers/lookup")]
    public async Task<IActionResult> LookupCustomer([FromQuery] string reference, CancellationToken ct)
    {
        try
        {
            var id = await _billing.FindCustomerIdByReferenceAsync(reference ?? string.Empty, ct);
            if (id is null)
            {
                // The client's own result: absent reference means not-found.
                return NotFound(new { category = "customer-not-found", message = "No customer exists for the given reference." });
            }

            return Ok(id.Value);
        }
        catch (BillingProviderException ex)
        {
            return ex.StatusCode switch
            {
                null => ex.InnerException switch
                {
                    HttpRequestException => StatusCode(503, new { category = "provider-unavailable", message = ex.Message }),
                    TaskCanceledException => StatusCode(504, new { category = "provider-timeout", message = ex.Message }),
                    JsonException => StatusCode(502, new { category = "provider-error", message = ex.Message }),
                    _ => NotFound(new { category = "customer-not-found", message = ex.Message })
                },
                400 => BadRequest(new { category = "invalid-request", message = ex.Message }),
                401 or 403 => StatusCode(502, new { category = "provider-authorization-failed", message = ex.Message }),
                404 => NotFound(new { category = "customer-not-found", message = ex.Message }),
                422 => UnprocessableEntity(new { category = "billing-rule-violation", message = ex.Message }),
                429 => StatusCode(429, new { category = "provider-rate-limited", message = ex.Message }),
                _ => StatusCode(502, new { category = "provider-error", message = ex.Message })
            };
        }
    }

    // GET /customers/{customer_id}/subscriptions.json
    [HttpGet("customers/{customerId}/subscriptions")]
    public async Task<IActionResult> GetCustomerSubscriptions(int customerId, CancellationToken ct)
    {
        try
        {
            return Ok(await _billing.GetSubscriptionsForCustomerAsync(customerId, ct));
        }
        catch (BillingProviderException ex)
        {
            return ex.StatusCode switch
            {
                null => ex.InnerException switch
                {
                    HttpRequestException => StatusCode(503, new { category = "provider-unavailable", message = ex.Message }),
                    TaskCanceledException => StatusCode(504, new { category = "provider-timeout", message = ex.Message }),
                    JsonException => StatusCode(502, new { category = "provider-error", message = ex.Message }),
                    _ => NotFound(new { category = "customer-not-found", message = ex.Message })
                },
                400 => BadRequest(new { category = "invalid-request", message = ex.Message }),
                401 or 403 => StatusCode(502, new { category = "provider-authorization-failed", message = ex.Message }),
                404 => NotFound(new { category = "customer-not-found", message = ex.Message }),
                422 => UnprocessableEntity(new { category = "billing-rule-violation", message = ex.Message }),
                429 => StatusCode(429, new { category = "provider-rate-limited", message = ex.Message }),
                _ => StatusCode(502, new { category = "provider-error", message = ex.Message })
            };
        }
    }

    // ---- Subscriptions -----------------------------------------------------
    // POST /subscriptions.json
    [HttpPost("subscriptions")]
    public async Task<IActionResult> CreateSubscription([FromBody] CreateSubscriptionRequest request, CancellationToken ct)
    {
        var s = request?.Subscription;
        try
        {
            var sub = await _billing.CreateSubscriptionAsync(s?.CustomerId ?? 0, s?.ProductHandle ?? string.Empty, ct);
            return Ok(sub);
        }
        catch (BillingProviderException ex)
        {
            return ex.StatusCode switch
            {
                null => ex.InnerException switch
                {
                    HttpRequestException => StatusCode(503, new { category = "provider-unavailable", message = ex.Message }),
                    TaskCanceledException => StatusCode(504, new { category = "provider-timeout", message = ex.Message }),
                    JsonException => StatusCode(502, new { category = "provider-error", message = ex.Message }),
                    _ => NotFound(new { category = "subscription-not-found", message = ex.Message })
                },
                400 => BadRequest(new { category = "invalid-request", message = ex.Message }),
                401 or 403 => StatusCode(502, new { category = "provider-authorization-failed", message = ex.Message }),
                404 => NotFound(new { category = "customer-not-found", message = ex.Message }),
                422 => UnprocessableEntity(new { category = "billing-rule-violation", message = ex.Message }),
                429 => StatusCode(429, new { category = "provider-rate-limited", message = ex.Message }),
                _ => StatusCode(502, new { category = "provider-error", message = ex.Message })
            };
        }
    }

    // GET /subscriptions/{subscription_id}.json
    [HttpGet("subscriptions/{subscriptionId}")]
    public async Task<IActionResult> GetSubscription(int subscriptionId, CancellationToken ct)
    {
        try
        {
            return Ok(await _billing.GetSubscriptionAsync(subscriptionId, ct));
        }
        catch (BillingProviderException ex)
        {
            return ex.StatusCode switch
            {
                null => ex.InnerException switch
                {
                    HttpRequestException => StatusCode(503, new { category = "provider-unavailable", message = ex.Message }),
                    TaskCanceledException => StatusCode(504, new { category = "provider-timeout", message = ex.Message }),
                    JsonException => StatusCode(502, new { category = "provider-error", message = ex.Message }),
                    _ => NotFound(new { category = "subscription-not-found", message = ex.Message })
                },
                400 => BadRequest(new { category = "invalid-request", message = ex.Message }),
                401 or 403 => StatusCode(502, new { category = "provider-authorization-failed", message = ex.Message }),
                404 => NotFound(new { category = "subscription-not-found", message = ex.Message }),
                422 => UnprocessableEntity(new { category = "billing-rule-violation", message = ex.Message }),
                429 => StatusCode(429, new { category = "provider-rate-limited", message = ex.Message }),
                _ => StatusCode(502, new { category = "provider-error", message = ex.Message })
            };
        }
    }

    // ---- Plan change (UC3) -------------------------------------------------
    // POST /subscriptions/{subscription_id}/migrations/preview.json
    [HttpPost("subscriptions/{subscriptionId}/migrations/preview")]
    public async Task<IActionResult> PreviewMigration(int subscriptionId, [FromBody] MigrationRequest request, CancellationToken ct)
    {
        var handle = request?.Migration?.ProductHandle ?? string.Empty;
        try
        {
            var preview = await _billing.PreviewPlanChangeAsync(subscriptionId, handle, PlanChangeTiming.Immediate, ct);
            return Ok(preview);
        }
        catch (BillingProviderException ex)
        {
            return ex.StatusCode switch
            {
                null => ex.InnerException switch
                {
                    HttpRequestException => StatusCode(503, new { category = "provider-unavailable", message = ex.Message }),
                    TaskCanceledException => StatusCode(504, new { category = "provider-timeout", message = ex.Message }),
                    JsonException => StatusCode(502, new { category = "provider-error", message = ex.Message }),
                    _ => NotFound(new { category = "subscription-not-found", message = ex.Message })
                },
                400 => BadRequest(new { category = "invalid-request", message = ex.Message }),
                401 or 403 => StatusCode(502, new { category = "provider-authorization-failed", message = ex.Message }),
                404 => NotFound(new { category = "subscription-not-found", message = ex.Message }),
                422 => UnprocessableEntity(new { category = "billing-rule-violation", message = ex.Message }),
                429 => StatusCode(429, new { category = "provider-rate-limited", message = ex.Message }),
                _ => StatusCode(502, new { category = "provider-error", message = ex.Message })
            };
        }
    }

    // POST /subscriptions/{subscription_id}/migrations.json (immediate, preserve_period=true)
    [HttpPost("subscriptions/{subscriptionId}/migrations")]
    public async Task<IActionResult> Migrate(int subscriptionId, [FromBody] MigrationRequest request, CancellationToken ct)
    {
        var handle = request?.Migration?.ProductHandle ?? string.Empty;
        try
        {
            var sub = await _billing.ChangePlanAsync(subscriptionId, handle, PlanChangeTiming.Immediate, ct);
            return Ok(sub);
        }
        catch (BillingProviderException ex)
        {
            return ex.StatusCode switch
            {
                null => ex.InnerException switch
                {
                    HttpRequestException => StatusCode(503, new { category = "provider-unavailable", message = ex.Message }),
                    TaskCanceledException => StatusCode(504, new { category = "provider-timeout", message = ex.Message }),
                    JsonException => StatusCode(502, new { category = "provider-error", message = ex.Message }),
                    _ => NotFound(new { category = "subscription-not-found", message = ex.Message })
                },
                400 => BadRequest(new { category = "invalid-request", message = ex.Message }),
                401 or 403 => StatusCode(502, new { category = "provider-authorization-failed", message = ex.Message }),
                404 => NotFound(new { category = "subscription-not-found", message = ex.Message }),
                422 => UnprocessableEntity(new { category = "billing-rule-violation", message = ex.Message }),
                429 => StatusCode(429, new { category = "provider-rate-limited", message = ex.Message }),
                _ => StatusCode(502, new { category = "provider-error", message = ex.Message })
            };
        }
    }

    // ---- Lifecycle (UC4) ---------------------------------------------------
    // POST /subscriptions/{subscription_id}/hold.json
    [HttpPost("subscriptions/{subscriptionId}/hold")]
    public async Task<IActionResult> Hold(int subscriptionId, CancellationToken ct)
    {
        try
        {
            var sub = await _billing.ApplyLifecycleActionAsync(subscriptionId, SubscriptionLifecycleAction.Pause, null, ct);
            return Ok(sub);
        }
        catch (BillingProviderException ex)
        {
            return ex.StatusCode switch
            {
                null => ex.InnerException switch
                {
                    HttpRequestException => StatusCode(503, new { category = "provider-unavailable", message = ex.Message }),
                    TaskCanceledException => StatusCode(504, new { category = "provider-timeout", message = ex.Message }),
                    JsonException => StatusCode(502, new { category = "provider-error", message = ex.Message }),
                    _ => NotFound(new { category = "subscription-not-found", message = ex.Message })
                },
                400 => BadRequest(new { category = "invalid-request", message = ex.Message }),
                401 or 403 => StatusCode(502, new { category = "provider-authorization-failed", message = ex.Message }),
                404 => NotFound(new { category = "subscription-not-found", message = ex.Message }),
                422 => UnprocessableEntity(new { category = "billing-rule-violation", message = ex.Message }),
                429 => StatusCode(429, new { category = "provider-rate-limited", message = ex.Message }),
                _ => StatusCode(502, new { category = "provider-error", message = ex.Message })
            };
        }
    }

    // POST /subscriptions/{subscription_id}/resume.json
    [HttpPost("subscriptions/{subscriptionId}/resume")]
    public async Task<IActionResult> Resume(int subscriptionId, CancellationToken ct)
    {
        try
        {
            var sub = await _billing.ApplyLifecycleActionAsync(subscriptionId, SubscriptionLifecycleAction.Resume, null, ct);
            return Ok(sub);
        }
        catch (BillingProviderException ex)
        {
            return ex.StatusCode switch
            {
                null => ex.InnerException switch
                {
                    HttpRequestException => StatusCode(503, new { category = "provider-unavailable", message = ex.Message }),
                    TaskCanceledException => StatusCode(504, new { category = "provider-timeout", message = ex.Message }),
                    JsonException => StatusCode(502, new { category = "provider-error", message = ex.Message }),
                    _ => NotFound(new { category = "subscription-not-found", message = ex.Message })
                },
                400 => BadRequest(new { category = "invalid-request", message = ex.Message }),
                401 or 403 => StatusCode(502, new { category = "provider-authorization-failed", message = ex.Message }),
                404 => NotFound(new { category = "subscription-not-found", message = ex.Message }),
                422 => UnprocessableEntity(new { category = "billing-rule-violation", message = ex.Message }),
                429 => StatusCode(429, new { category = "provider-rate-limited", message = ex.Message }),
                _ => StatusCode(502, new { category = "provider-error", message = ex.Message })
            };
        }
    }

    // DELETE /subscriptions/{subscription_id}.json (immediate cancel)
    [HttpDelete("subscriptions/{subscriptionId}")]
    public async Task<IActionResult> Cancel(int subscriptionId, [FromBody] CancelSubscriptionRequest? request, CancellationToken ct)
    {
        var reason = request?.Subscription?.CancellationMessage;
        try
        {
            var sub = await _billing.ApplyLifecycleActionAsync(subscriptionId, SubscriptionLifecycleAction.Cancel, reason, ct);
            return Ok(sub);
        }
        catch (BillingProviderException ex)
        {
            return ex.StatusCode switch
            {
                null => ex.InnerException switch
                {
                    HttpRequestException => StatusCode(503, new { category = "provider-unavailable", message = ex.Message }),
                    TaskCanceledException => StatusCode(504, new { category = "provider-timeout", message = ex.Message }),
                    JsonException => StatusCode(502, new { category = "provider-error", message = ex.Message }),
                    _ => NotFound(new { category = "subscription-not-found", message = ex.Message })
                },
                400 => BadRequest(new { category = "invalid-request", message = ex.Message }),
                401 or 403 => StatusCode(502, new { category = "provider-authorization-failed", message = ex.Message }),
                404 => NotFound(new { category = "subscription-not-found", message = ex.Message }),
                422 => UnprocessableEntity(new { category = "billing-rule-violation", message = ex.Message }),
                429 => StatusCode(429, new { category = "provider-rate-limited", message = ex.Message }),
                _ => StatusCode(502, new { category = "provider-error", message = ex.Message })
            };
        }
    }

    // PUT /subscriptions/{subscription_id}/reactivate.json
    [HttpPut("subscriptions/{subscriptionId}/reactivate")]
    public async Task<IActionResult> Reactivate(int subscriptionId, CancellationToken ct)
    {
        try
        {
            var sub = await _billing.ApplyLifecycleActionAsync(subscriptionId, SubscriptionLifecycleAction.Reactivate, null, ct);
            return Ok(sub);
        }
        catch (BillingProviderException ex)
        {
            return ex.StatusCode switch
            {
                null => ex.InnerException switch
                {
                    HttpRequestException => StatusCode(503, new { category = "provider-unavailable", message = ex.Message }),
                    TaskCanceledException => StatusCode(504, new { category = "provider-timeout", message = ex.Message }),
                    JsonException => StatusCode(502, new { category = "provider-error", message = ex.Message }),
                    _ => NotFound(new { category = "subscription-not-found", message = ex.Message })
                },
                400 => BadRequest(new { category = "invalid-request", message = ex.Message }),
                401 or 403 => StatusCode(502, new { category = "provider-authorization-failed", message = ex.Message }),
                404 => NotFound(new { category = "subscription-not-found", message = ex.Message }),
                422 => UnprocessableEntity(new { category = "billing-rule-violation", message = ex.Message }),
                429 => StatusCode(429, new { category = "provider-rate-limited", message = ex.Message }),
                _ => StatusCode(502, new { category = "provider-error", message = ex.Message })
            };
        }
    }

    // ---- Usage (UC2) -------------------------------------------------------
    // GET /components/lookup.json?handle={handle}
    [HttpGet("metered-component/verify")]
    public async Task<IActionResult> VerifyMeteredComponent(CancellationToken ct)
    {
        try
        {
            return Ok(await _billing.GetMeteredComponentAsync(ct));
        }
        catch (BillingProviderException ex)
        {
            return ex.StatusCode switch
            {
                null => ex.InnerException switch
                {
                    HttpRequestException => StatusCode(503, new { category = "provider-unavailable", message = ex.Message }),
                    TaskCanceledException => StatusCode(504, new { category = "provider-timeout", message = ex.Message }),
                    JsonException => StatusCode(502, new { category = "provider-error", message = ex.Message }),
                    _ => NotFound(new { category = "component-not-found", message = ex.Message })
                },
                400 => BadRequest(new { category = "invalid-request", message = ex.Message }),
                401 or 403 => StatusCode(502, new { category = "provider-authorization-failed", message = ex.Message }),
                404 => NotFound(new { category = "component-not-found", message = ex.Message }),
                422 => UnprocessableEntity(new { category = "billing-rule-violation", message = ex.Message }),
                429 => StatusCode(429, new { category = "provider-rate-limited", message = ex.Message }),
                _ => StatusCode(502, new { category = "provider-error", message = ex.Message })
            };
        }
    }

    // POST /subscriptions/{subscription_id}/components/{component_id}/usages.json
    // The client fixes the metered component from configuration, so {componentId} is an ignored path param.
    [HttpPost("subscriptions/{subscriptionId}/components/{componentId}/usages")]
    public async Task<IActionResult> RecordUsage(int subscriptionId, int componentId, [FromBody] RecordUsageRequest request, CancellationToken ct)
    {
        var usage = request?.Usage;
        try
        {
            await _billing.RecordUsageAsync(subscriptionId, usage?.Quantity ?? 0, usage?.Memo, ct);
            return Ok();
        }
        catch (BillingProviderException ex)
        {
            return ex.StatusCode switch
            {
                null => ex.InnerException switch
                {
                    HttpRequestException => StatusCode(503, new { category = "provider-unavailable", message = ex.Message }),
                    TaskCanceledException => StatusCode(504, new { category = "provider-timeout", message = ex.Message }),
                    JsonException => StatusCode(502, new { category = "provider-error", message = ex.Message }),
                    _ => NotFound(new { category = "subscription-not-found", message = ex.Message })
                },
                400 => BadRequest(new { category = "invalid-request", message = ex.Message }),
                401 or 403 => StatusCode(502, new { category = "provider-authorization-failed", message = ex.Message }),
                404 => NotFound(new { category = "subscription-not-found", message = ex.Message }),
                422 => UnprocessableEntity(new { category = "billing-rule-violation", message = ex.Message }),
                429 => StatusCode(429, new { category = "provider-rate-limited", message = ex.Message }),
                _ => StatusCode(502, new { category = "provider-error", message = ex.Message })
            };
        }
    }

    // GET /subscriptions/{subscription_id}/components/{component_id}.json
    // The client fixes the metered component from configuration; this route carries no component segment.
    [HttpGet("subscriptions/{subscriptionId}/component-balance")]
    public async Task<IActionResult> GetComponentBalance(int subscriptionId, CancellationToken ct)
    {
        try
        {
            return Ok(await _billing.GetUsageTotalAsync(subscriptionId, ct));
        }
        catch (BillingProviderException ex)
        {
            return ex.StatusCode switch
            {
                null => ex.InnerException switch
                {
                    HttpRequestException => StatusCode(503, new { category = "provider-unavailable", message = ex.Message }),
                    TaskCanceledException => StatusCode(504, new { category = "provider-timeout", message = ex.Message }),
                    JsonException => StatusCode(502, new { category = "provider-error", message = ex.Message }),
                    _ => NotFound(new { category = "subscription-not-found", message = ex.Message })
                },
                400 => BadRequest(new { category = "invalid-request", message = ex.Message }),
                401 or 403 => StatusCode(502, new { category = "provider-authorization-failed", message = ex.Message }),
                404 => NotFound(new { category = "subscription-not-found", message = ex.Message }),
                422 => UnprocessableEntity(new { category = "billing-rule-violation", message = ex.Message }),
                429 => StatusCode(429, new { category = "provider-rate-limited", message = ex.Message }),
                _ => StatusCode(502, new { category = "provider-error", message = ex.Message })
            };
        }
    }
}
