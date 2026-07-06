using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ReadSubscription)]
public class ReadSubscriptionTests
{
    [Fact]
    public async Task Known_subscription_returns_its_common_fields()
    {
        const string intent = "Read a known subscription's common fields";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.KnownActiveSubscriptionId));

        Expect.Status(response, HttpStatusCode.OK, intent);
        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;

        Expect.Field(root, "productHandle", TestSettings.KnownProductHandle, intent);
        Expect.State(root, "active", intent);
        Expect.NonBlankId(TestJson.GetSubscriptionId(root), "subscription id", intent);
    }

    [Fact]
    public async Task Unknown_subscription_yields_an_error_status()
    {
        const string intent = "Read an unknown subscription";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.UnknownSubscriptionId));

        Expect.Status(response, HttpStatusCode.NotFound, intent);
    }
}
