using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.PreviewMigration)]
public class PreviewPlanChangeTests : BlackBoxTest
{
    public PreviewPlanChangeTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Active_subscription_preview_returns_a_proration_quote()
    {
        const string intent = "Preview a plan change and get a proration quote";
        using var client = new ApiClient();
        var body = new { migration = new { product_handle = TestSettings.AlternateProductHandle }, timing = "Immediate" };

        var response = await client.PostAsync(TestSettings.MigrationsPreviewPath(TestSettings.KnownActiveSubscriptionId), body);

        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates a proration or plan-change amount (a charge and/or credit, in cents or " +
            "dollars) resulting from switching the subscription's plan."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Unknown_product_handle_yields_an_error_status()
    {
        const string intent = "Preview a plan change to an unknown product handle";
        using var client = new ApiClient();
        var body = new { migration = new { product_handle = TestSettings.UnknownProductHandle }, timing = "Immediate" };

        var response = await client.PostAsync(TestSettings.MigrationsPreviewPath(TestSettings.KnownActiveSubscriptionId), body);

        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the target product/plan does not exist / is invalid."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Non_active_subscription_preview_yields_an_error_status()
    {
        const string intent = "Preview a plan change on a non-active (canceled) subscription";
        using var client = new ApiClient();
        var body = new { migration = new { product_handle = TestSettings.AlternateProductHandle }, timing = "Immediate" };

        var response = await client.PostAsync(TestSettings.MigrationsPreviewPath(TestSettings.KnownCanceledSubscriptionId), body);

        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the subscription is not eligible for a plan-change preview " +
            "because it is not active."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Unknown_subscription_preview_yields_an_error_status()
    {
        const string intent = "Preview a plan change on an unknown subscription";
        using var client = new ApiClient();
        var body = new { migration = new { product_handle = TestSettings.AlternateProductHandle }, timing = "Immediate" };

        var response = await client.PostAsync(TestSettings.MigrationsPreviewPath(TestSettings.UnknownSubscriptionId), body);

        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the subscription was not found / does not exist."
        ]);
        Expect.AiPassed(report, intent);
    }
}
