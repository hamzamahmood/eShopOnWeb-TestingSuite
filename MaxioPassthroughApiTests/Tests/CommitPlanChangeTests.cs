using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.MigrateSubscription)]
public class CommitPlanChangeTests
{
    [SkippableFact]
    public async Task Active_subscription_migrates_to_a_different_known_product()
    {
        const string intent = "Migrate an active subscription to a different known product";
        using var client = new ApiClient();
        var body = new { migration = new { product_handle = TestSettings.AlternateProductHandle }, timing = "Immediate" };

        var response = await client.PostAsync(TestSettings.MigrationsPath(TestSettings.KnownActiveSubscriptionId), body);

        Expect.Status(response, HttpStatusCode.OK, intent);
        using var doc = JsonDocument.Parse(response.Body);
        Expect.Field(doc.RootElement, "productHandle", TestSettings.AlternateProductHandle, intent);
    }

    [SkippableFact]
    public async Task Unknown_product_handle_yields_an_error_status()
    {
        const string intent = "Migrate a subscription to an unknown product handle";
        using var client = new ApiClient();
        var body = new { migration = new { product_handle = TestSettings.UnknownProductHandle }, timing = "Immediate" };

        var response = await client.PostAsync(TestSettings.MigrationsPath(TestSettings.KnownActiveSubscriptionId), body);

        Expect.Status(response, HttpStatusCode.UnprocessableEntity, intent);
    }

    [SkippableFact]
    public async Task Canceled_subscription_cannot_be_migrated()
    {
        const string intent = "Migrate a canceled subscription";
        using var client = new ApiClient();
        var body = new { migration = new { product_handle = TestSettings.AlternateProductHandle }, timing = "Immediate" };

        var response = await client.PostAsync(TestSettings.MigrationsPath(TestSettings.KnownCanceledSubscriptionId), body);

        Expect.Status(response, HttpStatusCode.UnprocessableEntity, intent);
    }
}
