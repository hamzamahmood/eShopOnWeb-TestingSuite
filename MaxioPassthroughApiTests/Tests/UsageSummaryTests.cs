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
}
