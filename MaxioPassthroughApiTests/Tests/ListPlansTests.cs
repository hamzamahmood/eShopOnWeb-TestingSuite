using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ListProducts)]
public class ListPlansTests
{
    [Fact]
    public async Task ListPlans_returns_the_configured_familys_plans_with_common_fields()
    {
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.ListPlansPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.ContentType);

        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;

        // Flattened DTO shape: a bare JSON array of plan objects (no Maxio "product" envelope).
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(2, root.GetArrayLength());

        var plans = root.EnumerateArray().ToList();

        // Both mock products are present, keyed by their handle (a field both integrations expose).
        var handles = plans.Select(p => p.GetProperty("handle").GetString()).ToHashSet();
        Assert.Contains("zero-dollar-product", handles);
        Assert.Contains("gold", handles);

        // Verify the Gold Plan's common fields survive the flattening intact.
        var gold = plans.Single(p => p.GetProperty("handle").GetString() == "gold");
        Assert.Equal("Gold Plan", gold.GetProperty("name").GetString());
        Assert.Equal(1, gold.GetProperty("intervalCount").GetInt32());
        Assert.Equal("month", gold.GetProperty("intervalUnit").GetString());
        Assert.True(gold.GetProperty("requiresPaymentMethod").GetBoolean());
    }
}
