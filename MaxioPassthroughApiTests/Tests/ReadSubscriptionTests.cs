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
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.KnownActiveSubscriptionId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;

        Assert.Equal(TestSettings.KnownProductHandle, root.GetProperty("productHandle").GetString());
        Assert.Equal("active", root.GetProperty("state").GetString(), ignoreCase: true);
        Assert.False(string.IsNullOrWhiteSpace(TestJson.GetSubscriptionId(root)));
    }

    [Fact]
    public async Task Unknown_subscription_yields_an_error_status()
    {
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.UnknownSubscriptionId));

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound,
            $"Expected 404 for an unknown subscription id, but got {(int)response.StatusCode}. Body: {response.Body}");
    }
}
