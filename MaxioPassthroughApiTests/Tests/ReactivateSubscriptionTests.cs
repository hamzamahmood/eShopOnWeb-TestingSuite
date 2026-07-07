using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ReactivateSub)]
public class ReactivateSubscriptionTests
{
    [SkippableFact]
    public async Task Canceled_subscription_is_reactivated()
    {
        const string intent = "Reactivate a canceled subscription";
        using var client = new ApiClient();

        var response = await client.PutAsync(TestSettings.ReactivateSubscriptionPath(TestSettings.KnownCanceledSubscriptionId));

        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
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

        // The mock's reactivate-ineligible response is always 422 (a 4xx-origin Maxio error), which both
        // integrations' exception mapping turns into 422 — verified live on both. No 502 path is reachable here.
        Expect.Status(response, HttpStatusCode.UnprocessableEntity, intent);
    }
}
