using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// Record usage — POST against the configured metered component for a subscription. Routes differ (Direct
/// has no component-id path segment; Plugin's is present but inert), so the path is built via
/// <see cref="TestSettings.RecordUsagePath"/> (configurable per integration, like ListPlansPath). Response
/// DTOs differ in the id field's name/type (Direct's <c>providerUsageId</c> long vs Plugin's
/// <c>usageId</c> string) — see <see cref="TestJson.GetUsageId"/>.
///
/// <para>
/// On Direct, recording usage first calls <c>readComponent</c> to verify the configured metered component
/// (see MaxioBillingClient.RecordUsageAsync) — the mock's <c>GET /product_families/{id}/components/{id}.json</c>
/// route must be reachable for this endpoint's success case to work at all.
/// </para>
/// </summary>
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
            response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadGateway,
            $"Expected 422 (Direct) or 502 (Plugin), got {(int)response.StatusCode}. Body: {response.Body}");
    }
}
