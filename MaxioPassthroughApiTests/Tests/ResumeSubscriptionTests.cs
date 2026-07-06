using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ResumeSubscription)]
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
            response.StatusCode is HttpStatusCode.UnprocessableEntity,
            $"Expected 422, got {(int)response.StatusCode}. Body: {response.Body}");
    }
}
