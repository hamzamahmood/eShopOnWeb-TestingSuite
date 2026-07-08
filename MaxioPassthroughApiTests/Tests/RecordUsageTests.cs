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
            "The recorded usage has a quantity of 42.",
            "The recorded usage has the memo 'black-box test run'.",
            "The response contains a non-blank unique usage identifier."
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

        // Unlike ReadSubscriptionAsync, neither integration's record-usage path classifies Maxio's 404 into a
        // typed not-found exception — both fall through to the generic BillingProviderException -> 422 mapping.
        // The mock itself returns a clean 404 here (see docs/maxio-mock-server-error-codes.md #12); it's just
        // unreachable through either controller. Verified live on both integrations.
        Expect.Status(response, HttpStatusCode.UnprocessableEntity, intent);
    }
}
