using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// GET /api/customer?reference={ref} — proxies Maxio's read-customer-by-reference lookup.
/// Success returns Maxio's full customer object; an unknown reference must pass Maxio's EXACT 404 through
/// (not the app's old 4xx→422 remap).
/// </summary>
public class CustomerLookupTests
{
    [Fact]
    public async Task Known_reference_returns_the_Maxio_customer_object()
    {
        using var client = new ApiClient();

        var response = await client.GetAsync($"/api/customer?reference={TestSettings.KnownCustomerReference}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.ContentType);

        using var doc = JsonDocument.Parse(response.Body);
        var customer = doc.RootElement.GetProperty("customer");

        Assert.Equal(98765, customer.GetProperty("id").GetInt32());
        Assert.Equal(TestSettings.KnownCustomerReference, customer.GetProperty("reference").GetString());
        Assert.Equal("John", customer.GetProperty("first_name").GetString());
        Assert.Equal("john.doe@example.com", customer.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Unknown_reference_passes_through_Maxios_exact_404()
    {
        using var client = new ApiClient();

        var response = await client.GetAsync($"/api/customer?reference={TestSettings.UnknownCustomerReference}");

        // The exact Maxio status must reach the caller — NOT a remapped 422/502.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var doc = JsonDocument.Parse(response.Body);
        var errors = doc.RootElement.GetProperty("errors");
        Assert.Equal(JsonValueKind.Array, errors.ValueKind);
        Assert.Contains(
            errors.EnumerateArray().Select(e => e.GetString()),
            message => message is not null && message.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }
}
