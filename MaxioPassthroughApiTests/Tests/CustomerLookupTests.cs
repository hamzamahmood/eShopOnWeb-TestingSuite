using System.Net;
using MaxioPassthroughApiTests.Ai;
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
    [SkippableFact]
    public async Task Known_reference_returns_the_customer_id()
    {
        const string intent = "Look up a customer by a known reference (Plugin-only endpoint)";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.CustomerLookupPath(TestSettings.KnownCustomerReference));

        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            $"The response identifies the customer whose id is {TestSettings.KnownCustomerId}."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Unknown_reference_yields_404_not_found()
    {
        const string intent = "Look up a customer by an unknown reference (Plugin-only endpoint)";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.CustomerLookupPath(TestSettings.UnknownCustomerReference));

        Expect.Status(response, HttpStatusCode.NotFound, intent);
    }
}
