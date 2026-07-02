using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Services.Maxio;

/// <summary>
/// Raw passthrough to Maxio for the test-harness controller. Issues the same GET requests as
/// <see cref="MaxioBillingClient"/> over a typed HttpClient configured identically (same base URL, Basic
/// auth, resilience pipeline), but returns the untouched response body + exact status code for EVERY
/// outcome — including Maxio 4xx/5xx — so no DTO flattening and no status remapping happen. Because it
/// returns rather than throws on provider errors, <c>ExceptionMiddleware</c>'s status remap never runs for
/// these routes. Only transport-level failures are turned into a synthesized 502.
/// </summary>
public class MaxioPassthroughClient : IMaxioPassthrough
{
    private readonly HttpClient _httpClient;
    private readonly MaxioSettings _settings;
    private readonly ILogger<MaxioPassthroughClient> _logger;

    public MaxioPassthroughClient(IOptions<MaxioSettings> settings, ILogger<MaxioPassthroughClient> logger)
    {
        _httpClient = new HttpClient() { BaseAddress = new Uri("http://localhost:8080") };
        _settings = settings.Value;
        _logger = logger;
    }

    public Task<MaxioRawResponse> ListPlansRawAsync(CancellationToken cancellationToken)
    {
        // Same product-family route the billing client uses (MaxioBillingClient.FamilyRoute()).
        var familyRoute = Uri.EscapeDataString($"handle:{_settings.ProductFamilyHandle}");
        return SendRawAsync($"product_families/{familyRoute}/products.json", cancellationToken);
    }

    public Task<MaxioRawResponse> LookupCustomerRawAsync(string reference, CancellationToken cancellationToken)
    {
        return SendRawAsync($"customers/lookup.json?reference={Uri.EscapeDataString(reference)}", cancellationToken);
    }

    public Task<MaxioRawResponse> ListCustomerSubscriptionsRawAsync(string customerId,
        CancellationToken cancellationToken)
    {
        return SendRawAsync($"customers/{Uri.EscapeDataString(customerId)}/subscriptions.json", cancellationToken);
    }

    private async Task<MaxioRawResponse> SendRawAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new MaxioRawResponse((int)response.StatusCode, body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // genuine caller cancellation — let it propagate.
        }
        catch (Exception ex)
        {
            // Transport failure, timeout, or open circuit — Maxio never produced a status. Synthesize a 502
            // JSON here so this too bypasses the status-remapping middleware.
            _logger.LogError(ex, "Maxio passthrough GET {Path} failed before a usable response was received", path);
            return new MaxioRawResponse(502, "{\"error\":\"The billing provider is currently unavailable.\"}");
        }
    }
}
