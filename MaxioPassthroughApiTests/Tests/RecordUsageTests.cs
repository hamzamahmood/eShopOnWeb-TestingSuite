using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.RecordUsage)]
public class RecordUsageTests
{
    [Fact]
    public async Task Known_subscription_records_usage_with_the_given_quantity_and_memo()
    {
        const string intent = "Record usage with a given quantity and memo on a known subscription";
        using var client = new ApiClient();
        var body = new { usage = new { quantity = 42m, memo = "black-box test run" } };

        var response = await client.PostAsync(TestSettings.RecordUsagePath(TestSettings.KnownActiveSubscriptionId), body);

        Expect.Status(response, HttpStatusCode.OK, intent);
        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;
        Expect.Equal(42m, root.GetProperty("quantity").GetDecimal(), "'quantity' field", intent);
        Expect.Field(root, "memo", "black-box test run", intent);
        Expect.NonBlankId(TestJson.GetUsageId(root), "usage id", intent);
    }

    [Fact]
    public async Task Unknown_subscription_yields_an_error_status()
    {
        const string intent = "Record usage on an unknown subscription";
        using var client = new ApiClient();
        var body = new { usage = new { quantity = 1m } };

        var response = await client.PostAsync(TestSettings.RecordUsagePath(TestSettings.UnknownSubscriptionId), body);

        Expect.Status(response, HttpStatusCode.NotFound, intent);
    }
}
