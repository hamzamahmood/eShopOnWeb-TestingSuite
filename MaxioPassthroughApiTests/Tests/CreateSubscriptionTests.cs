using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.CreateSubscription)]
public class CreateSubscriptionTests
{
    [Fact]
    public async Task Known_customer_and_product_creates_a_subscription()
    {
        const string intent = "Create a subscription for a known customer and product";
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

        Expect.Status(response, HttpStatusCode.Created, intent);

        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;
        Expect.Field(root, "productHandle", TestSettings.KnownProductHandle, intent);
        Expect.State(root, "active", intent);
        Expect.NonBlankId(TestJson.GetSubscriptionId(root), "subscription id", intent);
    }

    [Fact]
    public async Task Unknown_product_handle_yields_an_error_status()
    {
        const string intent = "Create a subscription with an unknown product handle";
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

        Expect.Status(response, HttpStatusCode.UnprocessableEntity, intent);
    }

    [Fact]
    public async Task Unknown_customer_id_yields_an_error_status()
    {
        const string intent = "Create a subscription for an unknown customer id";
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

        Expect.Status(response, HttpStatusCode.UnprocessableEntity, intent);
    }
}
