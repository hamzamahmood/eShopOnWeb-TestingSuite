using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ResumeSubscription)]
public class ResumeSubscriptionTests
{
    [SkippableFact]
    public async Task On_hold_subscription_is_resumed()
    {
        const string intent = "Resume an on-hold subscription";
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.ResumeSubscriptionPath(TestSettings.KnownOnHoldSubscriptionId));

        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The subscription's lifecycle state is active."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Active_subscription_cannot_be_resumed()
    {
        const string intent = "Resume a subscription that is already active";
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.ResumeSubscriptionPath(TestSettings.KnownActiveSubscriptionId));

        Expect.Status(response, HttpStatusCode.UnprocessableEntity, intent);
    }
}
