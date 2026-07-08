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

        Expect.Status(response, HttpStatusCode.UnprocessableEntity, intent);
    }

    [Trait(MaxioTraits.Api, MaxioTraits.DelayedCancel)]
    [SkippableFact]
    public async Task Active_subscription_is_canceled_at_end_of_period()
    {
        const string intent = "Schedule an active subscription to cancel at the end of the current period";
        using var client = new ApiClient();
        var body = new { subscription = new { cancellation_message = "No longer needed" }, timing = "EndOfPeriod" };
        var path = TestSettings.EndOfPeriodCancelPath(TestSettings.KnownActiveSubscriptionId);

        // Route and HTTP method diverge by integration (Plugin: DELETE + timing; Direct: POST delayed_cancel).
        var response = TestSettings.EndOfPeriodCancelMethod.Equals("POST", StringComparison.OrdinalIgnoreCase)
            ? await client.PostAsync(path, body)
            : await client.DeleteAsync(path, body);

        Expect.Status(response, HttpStatusCode.OK, intent);
    }
}
