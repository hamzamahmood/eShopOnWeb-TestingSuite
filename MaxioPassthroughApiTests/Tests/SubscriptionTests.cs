using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ListCustomerSubs)]
public class SubscriptionTests
{
    [Fact]
    public async Task Known_customer_returns_the_subscriptions_array_with_common_fields()
    {
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.CustomerSubscriptionsPath(TestSettings.KnownCustomerId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.ContentType);

        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;

        // Flattened DTO shape: a bare JSON array of subscription objects (no Maxio "subscription" envelope).
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(1, root.GetArrayLength());

        var subscription = root[0];
        Assert.Equal("gold", subscription.GetProperty("productHandle").GetString());

        // State casing differs by integration (Direct "active" vs Plugin "Active"); compare case-insensitively.
        Assert.Equal("active", subscription.GetProperty("state").GetString(), ignoreCase: true);

        // The next-assessment timestamp is present and non-null on both shapes.
        Assert.Equal(JsonValueKind.String, subscription.GetProperty("nextAssessmentAt").ValueKind);
    }

    [Fact]
    public async Task Unknown_customer_yields_an_error_status()
    {
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.CustomerSubscriptionsPath(TestSettings.UnknownCustomerId));

        Assert.True(
            response.StatusCode is HttpStatusCode.UnprocessableEntity,
            $"Expected 422 for an unknown customer id, but got {(int)response.StatusCode}. Body: {response.Body}");
    }
}
