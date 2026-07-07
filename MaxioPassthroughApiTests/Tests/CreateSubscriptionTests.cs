using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.CreateSubscription)]
public class CreateSubscriptionTests
{
    [SkippableFact]
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

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            $"The created subscription is for the product/plan with handle '{TestSettings.KnownProductHandle}'.",
            "The subscription's lifecycle state is active.",
            "The response contains a non-blank unique subscription identifier."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
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

    [SkippableFact]
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
