using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// Read subscription — GET /api/maxio/subscriptions/{subscriptionId}, identical route on both integrations.
/// Response is a flattened DTO (not Maxio's raw envelope); only fields common to both shapes are asserted
/// (see docs/maxio-billing-controller-comparison.md).
/// </summary>
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

        // Plugin explicitly catches a not-found subscription and throws SubscriptionNotFoundException ->
        // 404. Direct's client has no such special case, so a 4xx from Maxio becomes a generic
        // BillingProviderException -> 422 via its ExceptionMiddleware.
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity,
            $"Expected 404 (Plugin) or 422 (Direct) for an unknown subscription id, but got {(int)response.StatusCode}. Body: {response.Body}");
    }
}
