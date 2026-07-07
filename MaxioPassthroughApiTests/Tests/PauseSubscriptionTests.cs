using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.HoldSubscription)]
public class PauseSubscriptionTests
{
    [SkippableFact]
    public async Task Active_subscription_is_paused()
    {
        const string intent = "Pause an active subscription";
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.PauseSubscriptionPath(TestSettings.KnownActiveSubscriptionId));

        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The subscription's lifecycle state indicates it is on hold / paused (e.g. on_hold, OnHold)."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Already_on_hold_subscription_yields_an_error_status()
    {
        const string intent = "Pause a subscription that is already on hold";
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.PauseSubscriptionPath(TestSettings.KnownOnHoldSubscriptionId));

        Expect.Status(response, HttpStatusCode.UnprocessableEntity, intent);
    }
}
