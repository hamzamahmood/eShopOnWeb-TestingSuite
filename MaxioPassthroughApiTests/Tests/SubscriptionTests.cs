using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ListCustomerSubs)]
public class SubscriptionTests
{
    [SkippableFact]
    public async Task Known_customer_returns_the_subscriptions_array_with_common_fields()
    {
        const string intent = "List a known customer's subscriptions with their common fields";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.CustomerSubscriptionsPath(TestSettings.KnownCustomerId));

        Expect.Status(response, HttpStatusCode.OK, intent);
        Expect.ContentType(response, "application/json", intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response is a list containing exactly 1 subscription.",
            $"That subscription is for the product/plan with handle '{TestSettings.KnownProductHandle}'.",
            "That subscription's lifecycle state is active.",
            "That subscription has a non-empty next-assessment / next-billing timestamp."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Unknown_customer_yields_an_error_status()
    {
        const string intent = "List subscriptions for an unknown customer";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.CustomerSubscriptionsPath(TestSettings.UnknownCustomerId));

        Expect.Status(response, HttpStatusCode.UnprocessableEntity, intent);
    }
}
