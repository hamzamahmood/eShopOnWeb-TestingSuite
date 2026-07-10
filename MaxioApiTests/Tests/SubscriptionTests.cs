using System.Net;
using MaxioApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ListCustomerSubs)]
public class SubscriptionTests : BlackBoxTest
{
    public SubscriptionTests(ITestOutputHelper output) : base(output) { }

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
            "That subscription has a non-blank unique subscription identifier.",
            "That subscription's lifecycle state is active.",
            $"That subscription is for the product/plan with handle '{TestSettings.KnownProductHandle}'."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Known_customer_with_no_subscriptions_returns_an_empty_list()
    {
        const string intent = "List a known customer that has no subscriptions (empty collection)";
        using var client = new ApiClient();

        var response = await client.GetAsync(
            TestSettings.CustomerSubscriptionsPath(TestSettings.EmptySubscriptionsCustomerId));

        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response is a list/collection of subscriptions that is empty (contains zero subscriptions)."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Unknown_customer_yields_an_error_status()
    {
        const string intent = "List subscriptions for an unknown customer";
        using var client = new ApiClient();

        var response = await client.GetAsync(
            TestSettings.CustomerSubscriptionsPath(TestSettings.UnknownCustomerId));

        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the customer was not found / does not exist, or that their " +
            "subscriptions could not be listed."
        ]);
        Expect.AiPassed(report, intent);
    }
}
