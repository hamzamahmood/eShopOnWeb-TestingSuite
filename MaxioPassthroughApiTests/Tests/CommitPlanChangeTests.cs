using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// Commit plan change (immediate) — POST /api/maxio/subscriptions/{subscriptionId}/migrations, identical
/// route on both integrations. Plugin additionally requires a "timing" control field in the body; set to
/// "Immediate" here so both integrations invoke the same underlying migrateSubscriptionProduct operation
/// (Direct has no such field and silently ignores the extra property).
/// </summary>
public class CommitPlanChangeTests
{
    [Fact]
    public async Task Active_subscription_migrates_to_a_different_known_product()
    {
        using var client = new ApiClient();
        var body = new { migration = new { product_handle = TestSettings.AlternateProductHandle }, timing = "Immediate" };

        var response = await client.PostAsync(TestSettings.MigrationsPath(TestSettings.KnownActiveSubscriptionId), body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        Assert.Equal(TestSettings.AlternateProductHandle, doc.RootElement.GetProperty("productHandle").GetString());
    }

    [Fact]
    public async Task Unknown_product_handle_yields_an_error_status()
    {
        using var client = new ApiClient();
        var body = new { migration = new { product_handle = TestSettings.UnknownProductHandle }, timing = "Immediate" };

        var response = await client.PostAsync(TestSettings.MigrationsPath(TestSettings.KnownActiveSubscriptionId), body);

        Assert.True(
            response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadGateway,
            $"Expected 422 (Direct) or 502 (Plugin), got {(int)response.StatusCode}. Body: {response.Body}");
    }

    [Fact]
    public async Task Canceled_subscription_cannot_be_migrated()
    {
        using var client = new ApiClient();
        var body = new { migration = new { product_handle = TestSettings.AlternateProductHandle }, timing = "Immediate" };

        var response = await client.PostAsync(TestSettings.MigrationsPath(TestSettings.KnownCanceledSubscriptionId), body);

        Assert.True(
            response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadGateway,
            $"Expected 422 (Direct) or 502 (Plugin), got {(int)response.StatusCode}. Body: {response.Body}");
    }
}
