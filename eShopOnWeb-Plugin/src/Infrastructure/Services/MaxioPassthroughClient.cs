using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MaxioAdvancedBilling;
using MaxioAdvancedBilling.Core.ErrorResponse;
using MaxioAdvancedBilling.Core.Exceptions;
using MaxioAdvancedBilling.Errors;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Services;

/// <summary>
/// Raw passthrough to Maxio for the test-harness controller, via the SAME generated SDK client as
/// <see cref="MaxioBillingClient"/>. On success it serializes the SDK's response model back to JSON — the
/// models carry <c>[JsonPropertyName]</c> / string-enum / union converters, so System.Text.Json reproduces
/// Maxio's wire schema. On an <c>SdkException</c> it returns Maxio's exact status code + raw error body, so
/// 404/422/etc. pass through unchanged (no status remapping, no DTO flattening). Only transport-level
/// failures become a synthesized 502.
/// </summary>
public class MaxioPassthroughClient : IMaxioPassthrough
{
    private readonly MaxioAdvancedBillingClient _client;
    private readonly MaxioSettings _settings;
    private readonly IAppLogger<MaxioPassthroughClient> _logger;

    public MaxioPassthroughClient(MaxioAdvancedBillingClient client, IOptions<MaxioSettings> settings, IAppLogger<MaxioPassthroughClient> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<MaxioRawResponse> ListPlansRawAsync(CancellationToken cancellationToken)
    {
        try
        {
            var products = await _client.ProductFamilies.ListProductsForProductFamily(
                _settings.ProductFamilyId.ToString(CultureInfo.InvariantCulture),
                dateField: null, filter: null, startDate: null, endDate: null, startDatetime: null, endDatetime: null,
                includeArchived: false, include: null, ct: cancellationToken);
            return Ok(products);
        }
        catch (SdkException<ListProductsForProductFamilyError> ex)
        {
            return FromApiError(ex.Error, "list plans");
        }
    }

    public async Task<MaxioRawResponse> LookupCustomerRawAsync(string reference, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.Customers.ReadCustomerByReference(reference, cancellationToken);
            return Ok(response);
        }
        catch (SdkException<RawError> ex)
        {
            // 404 (customer not found) passes straight through with Maxio's own body/status.
            return FromRawError(ex.Error);
        }
    }

    public async Task<MaxioRawResponse> ListCustomerSubscriptionsRawAsync(string customerId, CancellationToken cancellationToken)
    {
        try
        {
            var responses = await _client.Customers.ListCustomerSubscriptions(ParseId(customerId), cancellationToken);
            return Ok(responses);
        }
        catch (SdkException<RawError> ex)
        {
            return FromRawError(ex.Error);
        }
    }

    // Serialize the SDK response with its compile-time type so all modeled fields (and their attribute-driven
    // snake_case names / enum / union converters) are emitted — reproducing Maxio's response schema.
    private static MaxioRawResponse Ok<T>(T payload) => new(200, JsonSerializer.Serialize(payload));

    private MaxioRawResponse FromRawError(RawError raw)
    {
        var body = SafeReadBody(raw);
        _logger.LogWarning("Maxio passthrough failed: HTTP {StatusCode} {Body}", (int)raw.StatusCode, body);
        return new MaxioRawResponse((int)raw.StatusCode, body);
    }

    private MaxioRawResponse FromApiError(ApiError error, string operation)
    {
        if (error.TryGetRawError(out var raw))
        {
            return FromRawError(raw);
        }

        _logger.LogWarning("Maxio passthrough {Operation} returned an unhandled error", operation);
        return new MaxioRawResponse(502, "{\"error\":\"The billing provider returned an unexpected error.\"}");
    }

    private static string SafeReadBody(RawError raw)
    {
        try
        {
            return raw.ReadAsString();
        }
        catch
        {
            return "{\"error\":\"<unreadable body>\"}";
        }
    }

    private static double ParseId(string id) => double.Parse(id, NumberStyles.Float, CultureInfo.InvariantCulture);
}
