using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

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
            response.StatusCode is HttpStatusCode.UnprocessableEntity,
            $"Expected 422, got {(int)response.StatusCode}. Body: {response.Body}");
    }
}
