using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// Pause subscription — POST /api/maxio/subscriptions/{subscriptionId}/hold, identical route on both
/// integrations. The body is entirely inert on both, so only the subscriptionId path segment is exercised.
/// </summary>
public class PauseSubscriptionTests
{
    [Fact]
    public async Task Active_subscription_is_paused()
    {
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.PauseSubscriptionPath(TestSettings.KnownActiveSubscriptionId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);

        // Not a plain case-insensitive compare: Direct forwards Maxio's raw "on_hold"; Plugin renders its
        // SubscriptionState enum as "OnHold" — see TestJson.StatesEqual.
        var state = doc.RootElement.GetProperty("state").GetString();
        Assert.True(TestJson.StatesEqual("on_hold", state ?? string.Empty), $"Expected an on-hold state, got '{state}'.");
    }

    [Fact]
    public async Task Already_on_hold_subscription_yields_an_error_status()
    {
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.PauseSubscriptionPath(TestSettings.KnownOnHoldSubscriptionId));

        Assert.True(
            response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadGateway,
            $"Expected 422 (Direct) or 502 (Plugin), got {(int)response.StatusCode}. Body: {response.Body}");
    }
}
