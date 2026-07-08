using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.CreateSubscription)]
public class CreateSubscriptionTests : BlackBoxTest
{
    public CreateSubscriptionTests(ITestOutputHelper output) : base(output) { }

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

        Expect.NotSuccess(response, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the product/plan does not exist / is invalid."
        ]);
        Expect.AiPassed(report, intent);
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

        Expect.NotSuccess(response, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the customer does not exist / must exist."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Missing_subscription_envelope_yields_an_error_status()
    {
        const string intent = "Create a subscription with no `subscription` envelope";
        using var client = new ApiClient();
        var body = new { };

        var response = await client.PostAsync(TestSettings.SubscriptionsPath, body);

        // With no subscription details the customer id defaults to 0 (unknown) — the provider rejects it.
        Expect.NotSuccess(response, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the subscription could not be created — the customer/product " +
            "details were missing or invalid."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableTheory]
    [MemberData(nameof(PaymentFailureHandles))]
    public async Task Payment_failure_handle_yields_an_actionable_error(string productHandle)
    {
        var intent = $"Create a subscription with payment-failure handle '{productHandle}'";
        using var client = new ApiClient();
        var body = new
        {
            subscription = new
            {
                customer_id = long.Parse(TestSettings.KnownCustomerId),
                product_handle = productHandle
            }
        };

        var response = await client.PostAsync(TestSettings.SubscriptionsPath, body);

        Expect.NotSuccess(response, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the subscription could not be created because of a payment or " +
            "card problem (e.g. the card was declined, is missing, or additional payment verification is " +
            "required). Any wording that conveys a payment/card failure satisfies this rule."
        ]);
        Expect.AiPassed(report, intent);
    }

    public static IEnumerable<object[]> PaymentFailureHandles() =>
        TestSettings.PaymentRequiredProductHandles.Select(handle => new object[] { handle });
}
