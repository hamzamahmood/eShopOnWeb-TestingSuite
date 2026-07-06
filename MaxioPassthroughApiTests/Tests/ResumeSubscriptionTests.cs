using System.Net;
using System.Text.Json;
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
        using var doc = JsonDocument.Parse(response.Body);
        Expect.State(doc.RootElement, "active", intent);
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
