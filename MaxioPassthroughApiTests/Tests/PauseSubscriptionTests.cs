using System.Net;
using System.Text.Json;
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
        using var doc = JsonDocument.Parse(response.Body);

        // Not a plain case-insensitive compare: Direct forwards Maxio's raw "on_hold"; Plugin renders its
        // SubscriptionState enum as "OnHold" — see TestJson.StatesEqual (wrapped by Expect.State).
        Expect.State(doc.RootElement, "on_hold", intent);
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
