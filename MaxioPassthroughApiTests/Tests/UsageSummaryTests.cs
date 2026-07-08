using MaxioPassthroughApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ReadSubscriptionComponent)]
public class UsageSummaryTests : BlackBoxTest
{
    public UsageSummaryTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Known_subscription_returns_its_component_usage_summary()
    {
        const string intent = "Read a subscription's metered-component usage summary / balance";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.UsageSummaryPath(TestSettings.KnownActiveSubscriptionId));

        Expect.Status(response, System.Net.HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response conveys a period-to-date usage quantity / balance of 42 for the metered component."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Unknown_subscription_yields_an_error_status()
    {
        const string intent = "Read the usage summary of an unknown subscription";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.UsageSummaryPath(TestSettings.UnknownSubscriptionId));

        // Route exists on the integration, so this is a genuine (JSON-body) client error, not a route-miss
        // skip. The exact code differs by integration's not-found mapping, so assert the 4xx family.
        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");
    }
}
