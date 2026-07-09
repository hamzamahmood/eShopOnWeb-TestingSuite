using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.RecordUsage)]
public class RecordUsageTests : BlackBoxTest
{
    public RecordUsageTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Known_subscription_records_usage_with_the_given_quantity_and_memo()
    {
        const string intent = "Record usage with a given quantity and memo on a known subscription";
        using var client = new ApiClient();
        var body = new { usage = new { quantity = 42m, memo = "black-box test run" } };

        var response = await client.PostAsync(TestSettings.RecordUsagePath(TestSettings.KnownActiveSubscriptionId), body);

        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        // Plugin returns the recorded usage's numeric id in the body; the Direct integration returns an empty
        // 200 body, so this content rule fails on Direct by design.
        var report = await ai.VerifyAsync(response.Body, [
            "The response contains a numeric usage/event identifier for the recorded usage."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Unknown_subscription_yields_an_error_status()
    {
        const string intent = "Record usage on an unknown subscription";
        using var client = new ApiClient();
        var body = new { usage = new { quantity = 1m } };

        var response = await client.PostAsync(TestSettings.RecordUsagePath(TestSettings.UnknownSubscriptionId), body);

        // The Plugin surfaces this provider error as 502; the Direct integration preserves the Maxio status
        // (a 4xx) and fails this assertion by design.
        Expect.Status(response, HttpStatusCode.BadGateway, intent); // 502 expected (Plugin); Direct 4xx fails by design

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that usage could not be recorded because the subscription was not " +
            "found / does not exist."
        ]);
        Expect.AiPassed(report, intent);
    }
}
