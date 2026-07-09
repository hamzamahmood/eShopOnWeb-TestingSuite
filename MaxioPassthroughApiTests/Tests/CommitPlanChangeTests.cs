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
            "The response returns the subscription with a non-blank unique subscription identifier.",
            $"The subscription is now on the product/plan with handle '{TestSettings.AlternateProductHandle}'.",
            "The subscription conveys a recurring product/plan price (any units — cents or dollars).",
            "The subscription's lifecycle state is conveyed as a descriptive text value (e.g. 'active'), not an opaque numeric code."
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

        Expect.Status(response, HttpStatusCode.BadGateway, intent); // 502 — Plugin surfaces provider errors as 502; Direct returns 4xx and fails here by design

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

        Expect.Status(response, HttpStatusCode.BadGateway, intent); // 502 — Plugin surfaces provider errors as 502; Direct returns 4xx and fails here by design

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

        Expect.Status(response, HttpStatusCode.BadGateway, intent); // 502 — Plugin surfaces provider errors as 502; Direct returns 4xx and fails here by design

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the subscription was not found / does not exist."
        ]);
        Expect.AiPassed(report, intent);
    }
}

