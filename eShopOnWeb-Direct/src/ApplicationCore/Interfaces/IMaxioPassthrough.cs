using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Test-harness seam that returns Maxio's response untouched (exact HTTP status + raw JSON body) for a
/// few read operations, bypassing the flattening/abstracting <see cref="IBillingClient"/> DTOs. Consumed by
/// the anonymous passthrough controller so the caller sees Maxio's exact schema and status code. The
/// implementation lives in Infrastructure (the provider mechanism — raw HttpClient — never leaks upward).
/// </summary>
public interface IMaxioPassthrough
{
    /// <summary>GET /product_families/{product_family}/products.json — verbatim.</summary>
    Task<MaxioRawResponse> ListPlansRawAsync(CancellationToken cancellationToken);

    /// <summary>GET /customers/lookup.json?reference={reference} — verbatim (404 passes through if absent).</summary>
    Task<MaxioRawResponse> LookupCustomerRawAsync(string reference, CancellationToken cancellationToken);

    /// <summary>GET /customers/{customerId}/subscriptions.json — verbatim.</summary>
    Task<MaxioRawResponse> ListCustomerSubscriptionsRawAsync(string customerId, CancellationToken cancellationToken);
}

/// <summary>Maxio's response passed through verbatim: the HTTP status code and the raw JSON body.</summary>
public record MaxioRawResponse(int StatusCode, string Json);
