using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ResumeSubscription)]
public class ResumeSubscriptionTests : BlackBoxTest
{
    public ResumeSubscriptionTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task On_hold_subscription_is_resumed()
    {
        const string intent = "Resume an on-hold subscription";
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.ResumeSubscriptionPath(TestSettings.KnownOnHoldSubscriptionId));

        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response returns the updated subscription with a non-blank unique subscription identifier.",
            "The subscription is active after resuming — e.g. an isActive/active flag is true, or the status " +
            "field denotes an active state."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Active_subscription_cannot_be_resumed()
    {
        const string intent = "Resume a subscription that is already active";
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.ResumeSubscriptionPath(TestSettings.KnownActiveSubscriptionId));

        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that only on-hold subscriptions can be resumed (this one is not on hold)."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Unknown_subscription_cannot_be_resumed()
    {
        const string intent = "Resume an unknown subscription";
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.ResumeSubscriptionPath(TestSettings.UnknownSubscriptionId));

        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the subscription was not found / does not exist."
        ]);
        Expect.AiPassed(report, intent);
    }
}
