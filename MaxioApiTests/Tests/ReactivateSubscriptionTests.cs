using System.Net;
using MaxioApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ReactivateSub)]
public class ReactivateSubscriptionTests : BlackBoxTest
{
    public ReactivateSubscriptionTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Canceled_subscription_is_reactivated()
    {
        const string intent = "Reactivate a canceled subscription";
        using var client = new ApiClient();

        var response = await client.PutAsync(TestSettings.ReactivateSubscriptionPath(TestSettings.KnownCanceledSubscriptionId));

        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response returns the updated subscription with a non-blank unique subscription identifier.",
            "The subscription's lifecycle state is active."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Active_subscription_cannot_be_reactivated()
    {
        const string intent = "Reactivate a subscription that is already active";
        using var client = new ApiClient();

        var response = await client.PutAsync(TestSettings.ReactivateSubscriptionPath(TestSettings.KnownActiveSubscriptionId));

        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the subscription cannot be reactivated because it is not in a " +
            "canceled/unpaid/trial-ended state (it is already active)."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Unknown_subscription_cannot_be_reactivated()
    {
        const string intent = "Reactivate an unknown subscription";
        using var client = new ApiClient();

        var response = await client.PutAsync(TestSettings.ReactivateSubscriptionPath(TestSettings.UnknownSubscriptionId));

        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the subscription was not found / does not exist."
        ]);
        Expect.AiPassed(report, intent);
    }
}
