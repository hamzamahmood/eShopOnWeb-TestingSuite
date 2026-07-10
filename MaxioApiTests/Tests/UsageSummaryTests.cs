using MaxioApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioApiTests.Tests;

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
        const string intent = "Read a usage summary / balance for an unknown subscription";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.UsageSummaryPath(TestSettings.UnknownSubscriptionId));

        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the usage summary/balance could not be read because the " +
            "subscription was not found / does not exist."
        ]);
        Expect.AiPassed(report, intent);
    }
}
