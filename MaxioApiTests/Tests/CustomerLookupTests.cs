using System.Net;
using MaxioApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioApiTests.Tests;

/// <summary>
/// The read-only customer-lookup endpoint (<c>GET /api/maxio/customers/lookup?reference=…</c>) is exposed by
/// the <b>Direct</b> integration only; the Plugin integration never routes it. On the Plugin the route is
/// absent, so the request hits the fallback and returns an empty-body 404 — which the suite's route-divergence
/// auto-skip (<see cref="Expect"/>) reports as Skipped, not a failure. On the Direct integration both cases
/// exercise the real endpoint.
/// </summary>
[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.LookupCustomer)]
public class CustomerLookupTests : BlackBoxTest
{
    public CustomerLookupTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Known_reference_returns_the_customer_id()
    {
        const string intent = "Look up a customer by a known reference (Direct-only endpoint)";
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
    public async Task Unknown_reference_yields_a_not_found_error()
    {
        const string intent = "Look up a customer by an unknown reference (Direct-only endpoint)";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.CustomerLookupPath(TestSettings.UnknownCustomerReference));

        // A bodied not-found (the route exists on Direct); auto-skips on Plugin (route absent -> empty 404).
        Expect.NotSuccess(response, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that no customer was found for the given reference."
        ]);
        Expect.AiPassed(report, intent);
    }
}
