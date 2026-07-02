using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// Create subscription — POST /api/maxio/subscriptions, identical route + request shape on both
/// integrations (subscription.customer_id + subscription.product_handle). Success status differs (Direct
/// 200 OK; Plugin 201 Created), so it is asserted as a set. Response body reuses the same flattened
/// subscription DTO as ReadSubscriptionTests — see docs/maxio-billing-controller-comparison.md.
/// </summary>
public class CreateSubscriptionTests
{
    [Fact]
    public async Task Known_customer_and_product_creates_a_subscription()
    {
        using var client = new ApiClient();
        var body = new
        {
            subscription = new
            {
                customer_id = long.Parse(TestSettings.KnownCustomerId),
                product_handle = TestSettings.KnownProductHandle
            }
        };

        var response = await client.PostAsync(TestSettings.SubscriptionsPath, body);

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
            $"Expected 200 (Direct) or 201 (Plugin), got {(int)response.StatusCode}. Body: {response.Body}");

        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;
        Assert.Equal(TestSettings.KnownProductHandle, root.GetProperty("productHandle").GetString());
        Assert.Equal("active", root.GetProperty("state").GetString(), ignoreCase: true);
        Assert.False(string.IsNullOrWhiteSpace(TestJson.GetSubscriptionId(root)));
    }

    [Fact]
    public async Task Unknown_product_handle_yields_an_error_status()
    {
        using var client = new ApiClient();
        var body = new
        {
            subscription = new
            {
                customer_id = long.Parse(TestSettings.KnownCustomerId),
                product_handle = TestSettings.UnknownProductHandle
            }
        };

        var response = await client.PostAsync(TestSettings.SubscriptionsPath, body);

        Assert.True(
            response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadGateway,
            $"Expected 422 (Direct) or 502 (Plugin), got {(int)response.StatusCode}. Body: {response.Body}");
    }

    [Fact]
    public async Task Unknown_customer_id_yields_an_error_status()
    {
        using var client = new ApiClient();
        var body = new
        {
            subscription = new
            {
                customer_id = long.Parse(TestSettings.UnknownCustomerId),
                product_handle = TestSettings.KnownProductHandle
            }
        };

        var response = await client.PostAsync(TestSettings.SubscriptionsPath, body);

        Assert.True(
            response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadGateway,
            $"Expected 422 (Direct) or 502 (Plugin), got {(int)response.StatusCode}. Body: {response.Body}");
    }
}
