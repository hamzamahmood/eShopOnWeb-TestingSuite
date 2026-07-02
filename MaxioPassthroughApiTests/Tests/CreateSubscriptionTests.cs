using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

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
            response.StatusCode is HttpStatusCode.Created,
            $"Expected 201, got {(int)response.StatusCode}. Body: {response.Body}");

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
            response.StatusCode is HttpStatusCode.UnprocessableEntity,
            $"Expected 422, got {(int)response.StatusCode}. Body: {response.Body}");
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
            response.StatusCode is HttpStatusCode.UnprocessableEntity,
            $"Expected 422, got {(int)response.StatusCode}. Body: {response.Body}");
    }
}
