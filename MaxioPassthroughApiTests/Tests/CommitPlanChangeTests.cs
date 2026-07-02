using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

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
            response.StatusCode is HttpStatusCode.UnprocessableEntity,
            $"Expected 422 (Direct), got {(int)response.StatusCode}. Body: {response.Body}");
    }

    [Fact]
    public async Task Canceled_subscription_cannot_be_migrated()
    {
        using var client = new ApiClient();
        var body = new { migration = new { product_handle = TestSettings.AlternateProductHandle }, timing = "Immediate" };

        var response = await client.PostAsync(TestSettings.MigrationsPath(TestSettings.KnownCanceledSubscriptionId), body);

        Assert.True(
            response.StatusCode is HttpStatusCode.UnprocessableEntity,
            $"Expected 422, got {(int)response.StatusCode}. Body: {response.Body}");
    }
}
