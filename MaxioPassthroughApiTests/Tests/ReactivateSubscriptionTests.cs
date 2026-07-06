using System.Net;
using System.Text.Json;
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
        using var doc = JsonDocument.Parse(response.Body);
        Expect.State(doc.RootElement, "active", intent);
    }

    [SkippableFact]
    public async Task Active_subscription_cannot_be_reactivated()
    {
        const string intent = "Reactivate a subscription that is already active";
        using var client = new ApiClient();

        var response = await client.PutAsync(TestSettings.ReactivateSubscriptionPath(TestSettings.KnownActiveSubscriptionId));

        Expect.StatusOneOf(response, intent, HttpStatusCode.UnprocessableEntity, HttpStatusCode.BadGateway);
    }
}
