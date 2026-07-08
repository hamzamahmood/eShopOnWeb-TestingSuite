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

    // Both integrations resolve the customer differently (Direct binds `customer_id`; the Plugin folds
    // find-or-create in via `customer_reference`/`customer_email`), so every create body carries all three —
    // each integration reads the field it needs and ignores the rest.
    private static object CreateBody(string productHandle) => new
    {
        subscription = new
        {
            customer_id = long.Parse(TestSettings.KnownCustomerId),
            customer_reference = TestSettings.KnownCustomerReference,
            customer_email = "known@example.com",
            product_handle = productHandle
        }
    };

    [SkippableFact]
    public async Task Known_customer_and_product_creates_a_subscription()
    {
        const string intent = "Create a subscription for a known customer and product";
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.SubscriptionsPath, CreateBody(TestSettings.KnownProductHandle));

        // Both integrations return 200 on a successful create (neither emits 201 in run_4).
        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            $"The subscription is for the product/plan with handle '{TestSettings.KnownProductHandle}'.",
            "The subscription's lifecycle state is active.",
            "The response contains a non-blank unique subscription identifier."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Unknown_product_handle_yields_a_client_error()
    {
        const string intent = "Create a subscription with an unknown product handle";
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.SubscriptionsPath, CreateBody(TestSettings.UnknownProductHandle));

        // A bad product handle is a caller mistake — it must surface as a 4xx client error. The Plugin returns
        // one; the Direct integration collapses every provider failure to 502 Bad Gateway (a 5xx) and fails here.
        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the subscription could not be created because the product/plan " +
            "does not exist / is invalid."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableTheory]
    [MemberData(nameof(PaymentFailureHandles))]
    public async Task Payment_failure_handle_yields_a_client_error(string productHandle)
    {
        var intent = $"Create a subscription with payment-failure handle '{productHandle}'";
        using var client = new ApiClient();

        var response = await client.PostAsync(TestSettings.SubscriptionsPath, CreateBody(productHandle));

        // A payment/card problem is the caller's to resolve — a 4xx, not a 5xx. The Plugin surfaces a client
        // error; the Direct integration returns 502 (a 5xx) and fails.
        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the subscription could not be created — for example because of a " +
            "payment/card problem or because the plan could not be used. Any wording conveying that the create " +
            "was rejected satisfies this rule."
        ]);
        Expect.AiPassed(report, intent);
    }

    public static IEnumerable<object[]> PaymentFailureHandles() =>
        TestSettings.PaymentRequiredProductHandles.Select(handle => new object[] { handle });
}
