using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.PublicApi;

/// <summary>
/// Anonymous test-harness surface that proxies three Maxio read operations and returns Maxio's EXACT
/// response body + HTTP status (no DTO flattening, no status remapping). Because <see cref="IMaxioPassthrough"/>
/// returns the provider's status/body instead of throwing on a 4xx/5xx, the <c>ExceptionMiddleware</c> status
/// remap is bypassed for these routes only. Test-only: <see cref="AllowAnonymousAttribute"/> is deliberate.
/// </summary>
[ApiController]
[AllowAnonymous]
public class MaxioPassthroughController : ControllerBase
{
    private readonly IMaxioPassthrough _passthrough;

    public MaxioPassthroughController(IMaxioPassthrough passthrough)
    {
        _passthrough = passthrough;
    }

    /// <summary>GET /api/listplans → Maxio GET /product_families/{family}/products.json</summary>
    [HttpGet("api/listplans")]
    public async Task<IActionResult> ListPlans(CancellationToken cancellationToken)
    {
        return Raw(await _passthrough.ListPlansRawAsync(cancellationToken));
    }

    /// <summary>GET /api/customer?reference={ref} → Maxio GET /customers/lookup.json?reference={ref}</summary>
    [HttpGet("api/customer")]
    public async Task<IActionResult> LookupCustomer([FromQuery] string? reference, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return BadRequestJson("The 'reference' query parameter is required.");
        }

        return Raw(await _passthrough.LookupCustomerRawAsync(reference, cancellationToken));
    }

    /// <summary>GET /api/subscription?customerId={id} → Maxio GET /customers/{id}/subscriptions.json</summary>
    [HttpGet("api/subscription")]
    public async Task<IActionResult> ListSubscriptions([FromQuery] string? customerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return BadRequestJson("The 'customerId' query parameter is required.");
        }

        return Raw(await _passthrough.ListCustomerSubscriptionsRawAsync(customerId, cancellationToken));
    }

    private static ContentResult Raw(MaxioRawResponse response) => new()
    {
        Content = response.Json,
        ContentType = "application/json",
        StatusCode = response.StatusCode
    };

    private static ContentResult BadRequestJson(string message) => new()
    {
        Content = $"{{\"error\":\"{message}\"}}",
        ContentType = "application/json",
        StatusCode = 400
    };
}
