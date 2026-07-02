using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// PARKED — the customer endpoint is out of scope pending a redesign (to be revisited separately).
///
/// <para>
/// The old passthrough route <c>GET /api/customer?reference={ref}</c> returned Maxio's full customer object.
/// The new <c>MaxioBillingController</c>s have no equivalent: Direct exposes only <c>POST /api/maxio/customers</c>
/// (find-or-create, returning a bare id), and Plugin exposes <c>GET /api/maxio/customers/lookup?reference=</c>
/// returning only <c>{ "customerId": "98765" }</c> (404 with an empty body when unknown). Neither returns the
/// full customer object these assertions check, and the shapes/routes differ between integrations — so these
/// tests cannot be salvaged by a parameter swap. They are skipped until the customer endpoint is designed.
/// </para>
/// </summary>
public class CustomerLookupTests
{
    private const string ParkedReason =
        "Customer endpoint out of scope pending redesign (Direct has no GET lookup; Plugin returns only {customerId}). Revisit separately.";

    [Fact(Skip = ParkedReason)]
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

    [Fact(Skip = ParkedReason)]
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
