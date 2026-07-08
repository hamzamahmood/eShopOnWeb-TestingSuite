using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.CancelSubscription)]
public class CancelSubscriptionTests : BlackBoxTest
{
    public CancelSubscriptionTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Active_subscription_is_canceled()
    {
        const string intent = "Cancel an active subscription immediately";
        using var client = new ApiClient();
        var body = new { subscription = new { cancellation_message = "No longer needed" }, timing = "Immediate" };

        var response = await client.DeleteAsync(TestSettings.SubscriptionPath(TestSettings.KnownActiveSubscriptionId), body);

        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The subscription's lifecycle state indicates it has been canceled."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Already_canceled_subscription_yields_an_error_status()
    {
        const string intent = "Cancel a subscription that is already canceled";
        using var client = new ApiClient();
        var body = new { subscription = new { cancellation_message = "No longer needed" }, timing = "Immediate" };

        var response = await client.DeleteAsync(TestSettings.SubscriptionPath(TestSettings.KnownCanceledSubscriptionId), body);

        // Note: the mock returns this particular error with a singular {"error":…} key (not {"errors":[…]}).
        // DELETE /subscriptions/{id}.json declares 404 and 422; an already-canceled subscription is the 422 case.
        Expect.Status(response, HttpStatusCode.UnprocessableEntity, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the cancellation was rejected / could not be completed (for " +
            "example because the subscription is already canceled). Any wording conveying the cancel failed " +
            "satisfies this rule."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Unknown_subscription_cannot_be_canceled()
    {
        const string intent = "Cancel an unknown subscription";
        using var client = new ApiClient();
        var body = new { subscription = new { cancellation_message = "No longer needed" }, timing = "Immediate" };

        var response = await client.DeleteAsync(TestSettings.SubscriptionPath(TestSettings.UnknownSubscriptionId), body);

        // DELETE /subscriptions/{id}.json declares 404 and 422; an unknown subscription is the 404 case.
        Expect.Status(response, HttpStatusCode.NotFound, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the cancellation failed (for example because the subscription was " +
            "not found / does not exist). Any wording conveying the cancel could not be completed satisfies this rule."
        ]);
        Expect.AiPassed(report, intent);
    }
}
