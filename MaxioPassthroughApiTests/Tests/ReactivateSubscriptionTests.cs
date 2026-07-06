using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ReactivateSub)]
public class ReactivateSubscriptionTests
{
    [Fact]
    public async Task Canceled_subscription_is_reactivated()
    {
        using var client = new ApiClient();

        var response = await client.PutAsync(TestSettings.ReactivateSubscriptionPath(TestSettings.KnownCanceledSubscriptionId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        Assert.Equal("active", doc.RootElement.GetProperty("state").GetString(), ignoreCase: true);
    }

    [Fact]
    public async Task Active_subscription_cannot_be_reactivated()
    {
        using var client = new ApiClient();

        var response = await client.PutAsync(TestSettings.ReactivateSubscriptionPath(TestSettings.KnownActiveSubscriptionId));

        Assert.True(
            response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadGateway,
            $"Expected 422, got {(int)response.StatusCode}. Body: {response.Body}");
    }
}
