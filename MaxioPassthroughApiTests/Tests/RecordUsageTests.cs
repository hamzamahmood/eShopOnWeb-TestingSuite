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
        var report = await ai.VerifyAsync(response.Body, [
            "The response contains a non-blank unique identifier for the recorded usage event.",
            "The recorded usage has a quantity of 42.",
            "The recorded usage has the memo 'black-box test run'."
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

        // The mock returns a clean 404 for an unknown subscription on the usage route, but neither integration
        // classifies it into a typed not-found (unlike a direct subscription read) — it surfaces as a generic
        // provider error. We gate loosely on any error status and let the LLM confirm the body's meaning.
        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that usage could not be recorded because the subscription was not " +
            "found / does not exist."
        ]);
        Expect.AiPassed(report, intent);
    }
}
