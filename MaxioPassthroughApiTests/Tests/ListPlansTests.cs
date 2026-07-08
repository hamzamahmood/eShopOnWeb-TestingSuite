using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ListProducts)]
public class ListPlansTests : BlackBoxTest
{
    public ListPlansTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task ListPlans_returns_the_configured_familys_plans_with_common_fields()
    {
        const string intent = "List the configured product family's plans with their common fields";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.ListPlansPath);

        Expect.Status(response, HttpStatusCode.OK, intent);
        Expect.ContentType(response, "application/json", intent);
        // Response-shape lever: a plan's price is exposed under the camelCase key `priceInCents` (ASP.NET's
        // default naming). An integration that opts into snake_case exposes `price_in_cents` instead and fails.
        Expect.JsonHasKey(response, "priceInCents", intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response is a list of exactly 2 plans.",
            "One plan has the handle 'zero-dollar-product'.",
            "One plan has the handle 'gold', is named 'Gold Plan', and has a billing interval of 1 month."
        ]);
        Expect.AiPassed(report, intent);
    }
}
