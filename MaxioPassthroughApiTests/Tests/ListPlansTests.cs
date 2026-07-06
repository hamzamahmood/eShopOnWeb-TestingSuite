using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ListProducts)]
public class ListPlansTests
{
    [SkippableFact]
    public async Task ListPlans_returns_the_configured_familys_plans_with_common_fields()
    {
        const string intent = "List the configured product family's plans with their common fields";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.ListPlansPath);

        Expect.Status(response, HttpStatusCode.OK, intent);
        Expect.ContentType(response, "application/json", intent);

        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;

        // Flattened DTO shape: a bare JSON array of plan objects (no Maxio "product" envelope).
        Expect.Equal(JsonValueKind.Array, root.ValueKind, "response shape", intent);
        Expect.Equal(2, root.GetArrayLength(), "plan array length", intent);

        var plans = root.EnumerateArray().ToList();

        // Both mock products are present, keyed by their handle (a field both integrations expose).
        var handles = plans.Select(p => p.GetProperty("handle").GetString()).ToHashSet();
        Expect.Contains("zero-dollar-product", handles, "plan handle", intent);
        Expect.Contains("gold", handles, "plan handle", intent);

        // Verify the Gold Plan's common fields survive the flattening intact.
        var gold = plans.Single(p => p.GetProperty("handle").GetString() == "gold");
        Expect.Field(gold, "name", "Gold Plan", intent);
        Expect.Equal(1, gold.GetProperty("intervalCount").GetInt32(), "'intervalCount' field", intent);
        Expect.Field(gold, "intervalUnit", "month", intent);
        Expect.Equal(true, gold.GetProperty("requiresPaymentMethod").GetBoolean(), "'requiresPaymentMethod' field", intent);
    }
}
