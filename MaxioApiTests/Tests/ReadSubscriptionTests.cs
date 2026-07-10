using System.Net;
using MaxioApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ReadSubscription)]
public class ReadSubscriptionTests : BlackBoxTest
{
    public ReadSubscriptionTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Known_subscription_returns_its_common_fields()
    {
        const string intent = "Read a known subscription's common fields";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.KnownActiveSubscriptionId));

        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response contains a non-blank unique subscription identifier.",
            "The subscription's lifecycle state is active.",
            $"The subscription is for the product/plan with handle '{TestSettings.KnownProductHandle}'.",
            "The subscription conveys a recurring product/plan price (any units — cents or dollars).",
            $"The subscription belongs to the customer with reference '{TestSettings.KnownCustomerReference}'.",
            "The subscription conveys when the current billing period ends (a date/time, any format)."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Assessing_state_subscription_is_read_and_its_state_surfaced()
    {
        const string intent = "Read a subscription in a plausible-but-unknown provider state (assessing)";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.UnknownStateSubscriptionId));

        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response is a single subscription with a non-blank unique subscription identifier.",
            "The response conveys some lifecycle state for the subscription (any state value is acceptable)."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Unknown_subscription_yields_an_error_status()
    {
        const string intent = "Read an unknown subscription";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.UnknownSubscriptionId));

        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the subscription was not found / does not exist."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Non_numeric_subscription_id_is_rejected_with_a_client_error()
    {
        const string intent = "Reject a non-numeric subscription id with a client error";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.NonNumericSubscriptionId));

        Expect.NotSuccess(response, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the supplied subscription id is not a valid integer / is invalid."
        ]);
        Expect.AiPassed(report, intent);
    }
}
