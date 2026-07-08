using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ReadSubscription)]
public class ReadSubscriptionTests : BlackBoxTest
{
    public ReadSubscriptionTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Known_subscription_returns_its_common_fields()
    {
        const string intent = "Read a known subscription's common fields";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.KnownActiveSubscriptionId));

        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            $"The subscription is for the product/plan with handle '{TestSettings.KnownProductHandle}'.",
            "The subscription's lifecycle state is active.",
            "The response contains a non-blank unique subscription identifier."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Unknown_subscription_yields_an_error_status()
    {
        const string intent = "Read an unknown subscription";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.UnknownSubscriptionId));

        Expect.Status(response, HttpStatusCode.NotFound, intent);
    }
}
