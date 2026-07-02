using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

public class RecordUsageTests
{
    [Fact]
    public async Task Known_subscription_records_usage_with_the_given_quantity_and_memo()
    {
        using var client = new ApiClient();
        var body = new { usage = new { quantity = 42m, memo = "black-box test run" } };

        var response = await client.PostAsync(TestSettings.RecordUsagePath(TestSettings.KnownActiveSubscriptionId), body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;
        Assert.Equal(42m, root.GetProperty("quantity").GetDecimal());
        Assert.Equal("black-box test run", root.GetProperty("memo").GetString());
        Assert.False(string.IsNullOrWhiteSpace(TestJson.GetUsageId(root)));
    }

    [Fact]
    public async Task Unknown_subscription_yields_an_error_status()
    {
        using var client = new ApiClient();
        var body = new { usage = new { quantity = 1m } };

        var response = await client.PostAsync(TestSettings.RecordUsagePath(TestSettings.UnknownSubscriptionId), body);

        Assert.True(
            response.StatusCode is HttpStatusCode.UnprocessableEntity,
            $"Expected 422, got {(int)response.StatusCode}. Body: {response.Body}");
    }
}
