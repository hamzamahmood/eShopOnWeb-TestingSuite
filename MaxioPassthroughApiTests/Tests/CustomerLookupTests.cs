using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// The read-only customer-lookup endpoint (<c>GET /api/maxio/customers/lookup?reference=…</c>) is a
/// <b>Plugin-only</b> capability: with the SDK it costs a handful of lines, so the Plugin exposes it, but the
/// Direct integration never built it. These tests assert the Plugin's behavior and are designed to PASS on
/// Plugin and FAIL on Direct — specifically the known-reference case, which 404s on Direct because the route
/// is absent. (The unknown-reference case happens to be 404 on Direct too, but only coincidentally — every
/// path under a missing route is a 404 there.)
/// </summary>
[Trait(MaxioTraits.Category, MaxioTraits.CategoryPluginAdvantage)]
[Trait(MaxioTraits.Api, MaxioTraits.LookupCustomer)]
public class CustomerLookupTests
{
    [Fact]
    public async Task Known_reference_returns_the_customer_id()
    {
        const string intent = "Look up a customer by a known reference (Plugin-only endpoint)";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.CustomerLookupPath(TestSettings.KnownCustomerReference));

        Expect.Status(response, HttpStatusCode.OK, intent);
        using var doc = JsonDocument.Parse(response.Body);
        Expect.Equal(TestSettings.KnownCustomerId, TestJson.GetCustomerId(doc.RootElement), "customer id", intent);
    }

    [Fact]
    public async Task Unknown_reference_yields_404_not_found()
    {
        const string intent = "Look up a customer by an unknown reference (Plugin-only endpoint)";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.CustomerLookupPath(TestSettings.UnknownCustomerReference));

        Expect.Status(response, HttpStatusCode.NotFound, intent);
    }
}
