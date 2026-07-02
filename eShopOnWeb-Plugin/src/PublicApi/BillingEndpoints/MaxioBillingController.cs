using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

namespace Microsoft.eShopWeb.PublicApi.BillingEndpoints;

/// <summary>
/// HTTP microservice facade over <see cref="IBillingClient"/> (the Maxio Advanced Billing integration).
/// Every action maps one-to-one to a single <see cref="IBillingClient"/> method / Maxio endpoint call:
/// requests are bound and validated here, ApplicationCore DTOs are returned verbatim on success, and the
/// typed ApplicationCore exceptions are translated to RFC 7807 <see cref="ProblemDetails"/> responses
/// explicitly in <see cref="TryMapError"/> (rather than relying on the global ExceptionMiddleware), so the
/// controller is a self-contained description of the service's HTTP contract.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/billing")]
[Produces("application/json")]
public class MaxioBillingController : ControllerBase
{
    private readonly IBillingClient _billing;

    public MaxioBillingController(IBillingClient billing)
    {
        _billing = billing;
    }

    /// <summary><c>GET /api/billing/plans</c> → <see cref="IBillingClient.ListPlansAsync"/>.</summary>
    [HttpGet("plans")]
    [ProducesResponseType(typeof(IReadOnlyList<PlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ListPlans(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _billing.ListPlansAsync(cancellationToken));
        }
        catch (Exception ex) when (TryMapError(ex, out var error))
        {
            return error;
        }
    }

    /// <summary>
    /// <c>GET /api/billing/customers?reference={ref}</c> → <see cref="IBillingClient.FindCustomerIdAsync"/>.
    /// Read-only lookup; returns 404 when no customer exists for the reference (never creates one).
    /// </summary>
    [HttpGet("customers")]
    [ProducesResponseType(typeof(CustomerIdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> FindCustomer([FromQuery] string? reference, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return ValidationError(nameof(reference), "The 'reference' query parameter is required.");
        }

        try
        {
            var customerId = await _billing.FindCustomerIdAsync(reference, cancellationToken);
            if (customerId is null)
            {
                return Problem(
                    detail: $"No customer found for reference '{reference}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Customer not found");
            }

            return Ok(new CustomerIdResponse(reference, customerId));
        }
        catch (Exception ex) when (TryMapError(ex, out var error))
        {
            return error;
        }
    }

    /// <summary>
    /// <c>POST /api/billing/customers</c> → <see cref="IBillingClient.FindOrCreateCustomerAsync"/>.
    /// Idempotent: repeated calls for the same reference return the same customer id without creating a
    /// duplicate, so this returns 200 (the caller cannot rely on having created the record).
    /// </summary>
    [HttpPost("customers")]
    [ProducesResponseType(typeof(CustomerIdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> FindOrCreateCustomer([FromBody] FindOrCreateCustomerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var customerId = await _billing.FindOrCreateCustomerAsync(
                request.CustomerReference, request.Email, request.FirstName, request.LastName, cancellationToken);
            return Ok(new CustomerIdResponse(request.CustomerReference, customerId));
        }
        catch (Exception ex) when (TryMapError(ex, out var error))
        {
            return error;
        }
    }

    /// <summary><c>POST /api/billing/subscriptions</c> → <see cref="IBillingClient.CreateSubscriptionAsync"/>.</summary>
    [HttpPost("subscriptions")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> CreateSubscription([FromBody] CreateSubscriptionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var subscription = await _billing.CreateSubscriptionAsync(request.ProviderCustomerId, request.ProductHandle, cancellationToken);
            return CreatedAtAction(nameof(ReadSubscription), new { subscriptionId = subscription.SubscriptionId }, subscription);
        }
        catch (Exception ex) when (TryMapError(ex, out var error))
        {
            return error;
        }
    }

    /// <summary><c>GET /api/billing/subscriptions/{id}</c> → <see cref="IBillingClient.ReadSubscriptionAsync"/>.</summary>
    [HttpGet("subscriptions/{subscriptionId}")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ReadSubscription(string subscriptionId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _billing.ReadSubscriptionAsync(subscriptionId, cancellationToken));
        }
        catch (Exception ex) when (TryMapError(ex, out var error))
        {
            return error;
        }
    }

    /// <summary>
    /// <c>GET /api/billing/customers/{customerId}/subscriptions</c> →
    /// <see cref="IBillingClient.ListCustomerSubscriptionsAsync"/>. The path segment is the provider
    /// customer id (numeric), not the reference used by the lookup endpoint.
    /// </summary>
    [HttpGet("customers/{customerId}/subscriptions")]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ListCustomerSubscriptions(string customerId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _billing.ListCustomerSubscriptionsAsync(customerId, cancellationToken));
        }
        catch (Exception ex) when (TryMapError(ex, out var error))
        {
            return error;
        }
    }

    /// <summary>
    /// <c>POST /api/billing/metered-component/verify</c> → <see cref="IBillingClient.VerifyMeteredComponentAsync"/>.
    /// 204 when the configured metered component is valid; 502 when it is missing/misconfigured.
    /// </summary>
    [HttpPost("metered-component/verify")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> VerifyMeteredComponent(CancellationToken cancellationToken)
    {
        try
        {
            await _billing.VerifyMeteredComponentAsync(cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (TryMapError(ex, out var error))
        {
            return error;
        }
    }

    /// <summary><c>POST /api/billing/subscriptions/{id}/usage</c> → <see cref="IBillingClient.RecordUsageAsync"/>.</summary>
    [HttpPost("subscriptions/{subscriptionId}/usage")]
    [ProducesResponseType(typeof(UsageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> RecordUsage(string subscriptionId, [FromBody] RecordUsageRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var usage = await _billing.RecordUsageAsync(subscriptionId, request.Quantity, request.Memo, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, usage);
        }
        catch (Exception ex) when (TryMapError(ex, out var error))
        {
            return error;
        }
    }

    /// <summary><c>GET /api/billing/subscriptions/{id}/usage</c> → <see cref="IBillingClient.GetUsageSummaryAsync"/>.</summary>
    [HttpGet("subscriptions/{subscriptionId}/usage")]
    [ProducesResponseType(typeof(UsageSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetUsageSummary(string subscriptionId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _billing.GetUsageSummaryAsync(subscriptionId, cancellationToken));
        }
        catch (Exception ex) when (TryMapError(ex, out var error))
        {
            return error;
        }
    }

    /// <summary>
    /// <c>POST /api/billing/subscriptions/{id}/plan-change/preview</c> →
    /// <see cref="IBillingClient.PreviewPlanChangeAsync"/>. Quotes the cost of a plan change without applying it.
    /// </summary>
    [HttpPost("subscriptions/{subscriptionId}/plan-change/preview")]
    [ProducesResponseType(typeof(PlanChangeQuoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> PreviewPlanChange(string subscriptionId, [FromBody] PlanChangeRequest request, CancellationToken cancellationToken)
    {
        if (!TryParsePlanChangeTiming(request.Timing, out var timing, out var validation))
        {
            return validation;
        }

        try
        {
            return Ok(await _billing.PreviewPlanChangeAsync(subscriptionId, request.TargetProductHandle, timing, cancellationToken));
        }
        catch (Exception ex) when (TryMapError(ex, out var error))
        {
            return error;
        }
    }

    /// <summary>
    /// <c>POST /api/billing/subscriptions/{id}/plan-change</c> → <see cref="IBillingClient.CommitPlanChangeAsync"/>.
    /// </summary>
    [HttpPost("subscriptions/{subscriptionId}/plan-change")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> CommitPlanChange(string subscriptionId, [FromBody] PlanChangeRequest request, CancellationToken cancellationToken)
    {
        if (!TryParsePlanChangeTiming(request.Timing, out var timing, out var validation))
        {
            return validation;
        }

        try
        {
            return Ok(await _billing.CommitPlanChangeAsync(subscriptionId, request.TargetProductHandle, timing, cancellationToken));
        }
        catch (Exception ex) when (TryMapError(ex, out var error))
        {
            return error;
        }
    }

    /// <summary><c>POST /api/billing/subscriptions/{id}/pause</c> → <see cref="IBillingClient.PauseAsync"/>.</summary>
    [HttpPost("subscriptions/{subscriptionId}/pause")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Pause(string subscriptionId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _billing.PauseAsync(subscriptionId, cancellationToken));
        }
        catch (Exception ex) when (TryMapError(ex, out var error))
        {
            return error;
        }
    }

    /// <summary><c>POST /api/billing/subscriptions/{id}/resume</c> → <see cref="IBillingClient.ResumeAsync"/>.</summary>
    [HttpPost("subscriptions/{subscriptionId}/resume")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Resume(string subscriptionId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _billing.ResumeAsync(subscriptionId, cancellationToken));
        }
        catch (Exception ex) when (TryMapError(ex, out var error))
        {
            return error;
        }
    }

    /// <summary><c>POST /api/billing/subscriptions/{id}/cancel</c> → <see cref="IBillingClient.CancelAsync"/>.</summary>
    [HttpPost("subscriptions/{subscriptionId}/cancel")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Cancel(string subscriptionId, [FromBody] CancelSubscriptionRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseCancelTiming(request.Timing, out var timing, out var validation))
        {
            return validation;
        }

        try
        {
            return Ok(await _billing.CancelAsync(subscriptionId, timing, request.Reason, cancellationToken));
        }
        catch (Exception ex) when (TryMapError(ex, out var error))
        {
            return error;
        }
    }

    /// <summary><c>POST /api/billing/subscriptions/{id}/reactivate</c> → <see cref="IBillingClient.ReactivateAsync"/>.</summary>
    [HttpPost("subscriptions/{subscriptionId}/reactivate")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Reactivate(string subscriptionId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _billing.ReactivateAsync(subscriptionId, cancellationToken));
        }
        catch (Exception ex) when (TryMapError(ex, out var error))
        {
            return error;
        }
    }

    /// <summary>
    /// Translates the typed ApplicationCore exceptions the billing client raises into RFC 7807 responses.
    /// Returns <c>false</c> (leaving <paramref name="result"/> unset) for anything else, so genuinely
    /// unexpected exceptions propagate to the global ExceptionMiddleware and surface as a 500.
    /// </summary>
    private bool TryMapError(Exception exception, out IActionResult result)
    {
        switch (exception)
        {
            case SubscriptionNotFoundException:
                result = Problem(detail: exception.Message, statusCode: StatusCodes.Status404NotFound, title: "Subscription not found");
                return true;

            case PaymentVerificationRequiredException payment:
                // Surface Maxio's own validation messages alongside the safe user-facing message, matching
                // how the global ExceptionMiddleware renders this case.
                result = Problem(
                    detail: payment.Message + " " + string.Join(" ", payment.ProviderMessages),
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "Payment verification required");
                return true;

            case MeteredComponentMisconfiguredException:
                result = Problem(detail: exception.Message, statusCode: StatusCodes.Status502BadGateway, title: "Metered component misconfigured");
                return true;

            case BillingProviderException:
                // The provider could not complete the request (upstream error/timeout/undeclared response).
                // 502 Bad Gateway is the right signal for a facade whose upstream failed.
                result = Problem(detail: exception.Message, statusCode: StatusCodes.Status502BadGateway, title: "Billing provider error");
                return true;

            default:
                result = null!;
                return false;
        }
    }

    private bool TryParsePlanChangeTiming(string? value, out PlanChangeTiming timing, out IActionResult validation)
    {
        if (Enum.TryParse(value, ignoreCase: true, out timing) && Enum.IsDefined(typeof(PlanChangeTiming), timing))
        {
            validation = null!;
            return true;
        }

        timing = default;
        validation = ValidationError(nameof(PlanChangeRequest.Timing),
            $"'timing' must be one of: {string.Join(", ", Enum.GetNames(typeof(PlanChangeTiming)))}.");
        return false;
    }

    private bool TryParseCancelTiming(string? value, out CancelTiming timing, out IActionResult validation)
    {
        if (Enum.TryParse(value, ignoreCase: true, out timing) && Enum.IsDefined(typeof(CancelTiming), timing))
        {
            validation = null!;
            return true;
        }

        timing = default;
        validation = ValidationError(nameof(CancelSubscriptionRequest.Timing),
            $"'timing' must be one of: {string.Join(", ", Enum.GetNames(typeof(CancelTiming)))}.");
        return false;
    }

    private IActionResult ValidationError(string field, string message)
    {
        ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }
}
