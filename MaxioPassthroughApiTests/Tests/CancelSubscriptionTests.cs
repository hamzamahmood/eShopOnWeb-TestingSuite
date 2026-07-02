using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// Cancel subscription (immediate) — DELETE /api/maxio/subscriptions/{subscriptionId}, identical route on
/// both integrations. Plugin additionally requires a non-null body with a "timing" control field; set to
/// "Immediate" here so both integrations invoke the same underlying cancelSubscription operation (Direct's
/// DELETE endpoint is always immediate and ignores the extra field).
/// </summary>
public class CancelSubscriptionTests
{
    [Fact]
    public async Task Active_subscription_is_canceled()
    {
        using var client = new ApiClient();
        var body = new { subscription = new { cancellation_message = "No longer needed" }, timing = "Immediate" };

        var response = await client.DeleteAsync(TestSettings.SubscriptionPath(TestSettings.KnownActiveSubscriptionId), body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        Assert.Equal("canceled", doc.RootElement.GetProperty("state").GetString(), ignoreCase: true);
    }

    [Fact]
    public async Task Already_canceled_subscription_yields_an_error_status()
    {
        using var client = new ApiClient();
        var body = new { subscription = new { cancellation_message = "No longer needed" }, timing = "Immediate" };

        var response = await client.DeleteAsync(TestSettings.SubscriptionPath(TestSettings.KnownCanceledSubscriptionId), body);

        Assert.True(
            response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadGateway,
            $"Expected 422 (Direct) or 502 (Plugin), got {(int)response.StatusCode}. Body: {response.Body}");
    }
}
