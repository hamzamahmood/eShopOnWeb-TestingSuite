using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.MigrateSubscription)]
public class CommitPlanChangeTests : BlackBoxTest
{
    public CommitPlanChangeTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Active_subscription_migrates_to_a_different_known_product()
    {
        const string intent = "Migrate an active subscription to a different known product";
        using var client = new ApiClient();
        var body = new { migration = new { product_handle = TestSettings.AlternateProductHandle }, timing = "Immediate" };

        var response = await client.PostAsync(TestSettings.MigrationsPath(TestSettings.KnownActiveSubscriptionId), body);

        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            $"The subscription is now on the product/plan with handle '{TestSettings.AlternateProductHandle}'."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Unknown_product_handle_yields_an_error_status()
    {
        const string intent = "Migrate a subscription to an unknown product handle";
        using var client = new ApiClient();
        var body = new { migration = new { product_handle = TestSettings.UnknownProductHandle }, timing = "Immediate" };

        var response = await client.PostAsync(TestSettings.MigrationsPath(TestSettings.KnownActiveSubscriptionId), body);

        Expect.NotSuccess(response, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the target product/plan does not exist / is invalid."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Canceled_subscription_cannot_be_migrated()
    {
        const string intent = "Migrate a canceled subscription";
        using var client = new ApiClient();
        var body = new { migration = new { product_handle = TestSettings.AlternateProductHandle }, timing = "Immediate" };

        var response = await client.PostAsync(TestSettings.MigrationsPath(TestSettings.KnownCanceledSubscriptionId), body);

        Expect.NotSuccess(response, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the plan change was rejected because the subscription is not " +
            "active (e.g. it is canceled)."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Unknown_subscription_cannot_be_migrated()
    {
        const string intent = "Migrate an unknown subscription";
        using var client = new ApiClient();
        var body = new { migration = new { product_handle = TestSettings.AlternateProductHandle }, timing = "Immediate" };

        var response = await client.PostAsync(TestSettings.MigrationsPath(TestSettings.UnknownSubscriptionId), body);

        Expect.NotSuccess(response, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the subscription was not found / does not exist."
        ]);
        Expect.AiPassed(report, intent);
    }

    [Trait(MaxioTraits.Api, MaxioTraits.UpdateSubscription)]
    [SkippableFact]
    public async Task Active_subscription_plan_change_is_scheduled_at_renewal()
    {
        const string intent = "Schedule a plan change to take effect at the next renewal";
        using var client = new ApiClient();
        // Both wrapper shapes are supplied so one body serves both routes (each reads its own wrapper, ignores
        // the other): the Plugin's migrations route reads `migration`; the Direct PUT reads `subscription`.
        var body = new
        {
            migration = new { product_handle = TestSettings.AlternateProductHandle },
            subscription = new { product_handle = TestSettings.AlternateProductHandle },
            timing = "AtRenewal"
        };
        var path = TestSettings.ScheduleAtRenewalPath(TestSettings.KnownActiveSubscriptionId);

        // Route and HTTP method diverge by integration (Plugin: POST migrations + timing; Direct: PUT subscription).
        var response = TestSettings.ScheduleAtRenewalMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase)
            ? await client.PutAsync(path, body)
            : await client.PostAsync(path, body);

        Expect.Status(response, HttpStatusCode.OK, intent);
    }
}

