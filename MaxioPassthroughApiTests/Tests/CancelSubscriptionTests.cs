using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.CancelSubscription)]
public class CancelSubscriptionTests
{
    [Fact]
    public async Task Active_subscription_is_canceled()
    {
        const string intent = "Cancel an active subscription immediately";
        using var client = new ApiClient();
        var body = new { subscription = new { cancellation_message = "No longer needed" }, timing = "Immediate" };

        var response = await client.DeleteAsync(TestSettings.SubscriptionPath(TestSettings.KnownActiveSubscriptionId), body);

        Expect.Status(response, HttpStatusCode.OK, intent);
        using var doc = JsonDocument.Parse(response.Body);
        Expect.State(doc.RootElement, "canceled", intent);
    }

    [Fact]
    public async Task Already_canceled_subscription_yields_an_error_status()
    {
        const string intent = "Cancel a subscription that is already canceled";
        using var client = new ApiClient();
        var body = new { subscription = new { cancellation_message = "No longer needed" }, timing = "Immediate" };

        var response = await client.DeleteAsync(TestSettings.SubscriptionPath(TestSettings.KnownCanceledSubscriptionId), body);

        Expect.Status(response, HttpStatusCode.UnprocessableEntity, intent);
    }
}
