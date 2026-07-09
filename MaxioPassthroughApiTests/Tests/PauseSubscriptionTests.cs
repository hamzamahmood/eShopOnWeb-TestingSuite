using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.HoldSubscription)]
public class PauseSubscriptionTests : BlackBoxTest
{
    public PauseSubscriptionTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Active_subscription_is_paused()
    {
        const string intent = "Pause an active subscription";
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.PauseSubscriptionPath(TestSettings.KnownActiveSubscriptionId));

        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response returns the updated subscription with a non-blank unique subscription identifier.",
            "The subscription's lifecycle state indicates it is paused / on hold."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Already_on_hold_subscription_yields_an_error_status()
    {
        const string intent = "Pause a subscription that is already on hold";
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.PauseSubscriptionPath(TestSettings.KnownOnHoldSubscriptionId));

        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the subscription is not eligible to be put on hold / paused."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Canceled_subscription_cannot_be_paused()
    {
        const string intent = "Pause a canceled subscription";
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.PauseSubscriptionPath(TestSettings.KnownCanceledSubscriptionId));

        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the subscription cannot be put on hold / paused in its current state."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Unknown_subscription_cannot_be_paused()
    {
        const string intent = "Pause an unknown subscription";
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.PauseSubscriptionPath(TestSettings.UnknownSubscriptionId));

        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the subscription was not found / does not exist."
        ]);
        Expect.AiPassed(report, intent);
    }
}
