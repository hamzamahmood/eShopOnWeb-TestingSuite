using System.Net;
using MaxioApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioApiTests.Tests;

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

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response is a list of exactly 2 plans.",
            "Every plan in the list has a non-blank unique plan/product identifier.",
            "One plan has the handle 'zero-dollar-product'.",
            "One plan has the handle 'gold', is named 'Gold Plan', has a billing interval of 1 month, and has a price of 1000 cents (equivalently $10.00)."
        ]);
        Expect.AiPassed(report, intent);
    }
}
