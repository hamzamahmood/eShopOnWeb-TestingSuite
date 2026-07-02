using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// GET /api/listplans — proxies Maxio's list-products-for-product-family. This endpoint takes no caller
/// input (it uses the PublicApi's configured product family), so only the success path is covered — a
/// "wrong input parameter" failure can't be triggered from the request. The PublicApi must be configured
/// with the mock's known family (ProductFamilyId=527890 / ProductFamilyHandle=acme-projects).
/// </summary>
public class ListPlansTests
{
    [Fact]
    public async Task ListPlans_returns_the_Maxio_products_array_with_expected_fields()
    {
        using var client = new ApiClient();

        var response = await client.GetAsync("/api/listplans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.ContentType);

        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;

        // Maxio wire shape: an array of { "product": { ... } } envelopes.
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(2, root.GetArrayLength());

        var products = root.EnumerateArray().Select(e => e.GetProperty("product")).ToList();

        // Both mock products are present.
        var ids = products.Select(p => p.GetProperty("id").GetInt32()).ToHashSet();
        Assert.Contains(3801242, ids);
        Assert.Contains(3858146, ids);

        // Verify the response parameters of the Gold Plan survive the passthrough intact.
        var gold = products.Single(p => p.GetProperty("handle").GetString() == "gold");
        Assert.Equal(3858146, gold.GetProperty("id").GetInt32());
        Assert.Equal("Gold Plan", gold.GetProperty("name").GetString());
        Assert.Equal(1000, gold.GetProperty("price_in_cents").GetInt32());
        Assert.Equal("month", gold.GetProperty("interval_unit").GetString());
        Assert.Equal(527890, gold.GetProperty("product_family").GetProperty("id").GetInt32());
    }
}
