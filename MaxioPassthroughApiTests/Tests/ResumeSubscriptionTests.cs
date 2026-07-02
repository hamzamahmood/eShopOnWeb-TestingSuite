using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// Resume subscription — POST /api/maxio/subscriptions/{subscriptionId}/resume, identical route on both
/// integrations.
/// </summary>
public class ResumeSubscriptionTests
{
    [Fact]
    public async Task On_hold_subscription_is_resumed()
    {
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.ResumeSubscriptionPath(TestSettings.KnownOnHoldSubscriptionId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        Assert.Equal("active", doc.RootElement.GetProperty("state").GetString(), ignoreCase: true);
    }

    [Fact]
    public async Task Active_subscription_cannot_be_resumed()
    {
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.ResumeSubscriptionPath(TestSettings.KnownActiveSubscriptionId));

        Assert.True(
            response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadGateway,
            $"Expected 422 (Direct) or 502 (Plugin), got {(int)response.StatusCode}. Body: {response.Body}");
    }
}
