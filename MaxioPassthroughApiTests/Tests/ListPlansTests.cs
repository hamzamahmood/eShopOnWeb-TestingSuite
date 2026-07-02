using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// List plans — <c>MaxioBillingController</c> list-plans endpoint, identical route on both integrations
/// (<c>/api/maxio/product-families/{productFamilyId}/products</c>, see <see cref="TestSettings.ListPlansPath"/>).
/// This endpoint takes no meaningful caller input (the client always uses the PublicApi's configured
/// product family), so only the success path is covered. The PublicApi must be configured with the mock's
/// known family (ProductFamilyId=527890 / ProductFamilyHandle=acme-projects).
///
/// <para>
/// Unlike the old passthrough controller, this endpoint returns a FLATTENED, provider-agnostic DTO — not
/// Maxio's raw <c>{ "product": {...} }</c> envelope. The two integrations also return slightly different
/// shapes (Direct's <c>BillingPlan</c> carries <c>priceInCents</c> + <c>providerProductId</c>; Plugin's
/// <c>PlanDto</c> carries <c>price</c> in dollars and no id). This test therefore asserts only the fields
/// common to BOTH shapes: <c>handle</c>, <c>name</c>, <c>intervalCount</c>, <c>intervalUnit</c>,
/// <c>requiresPaymentMethod</c>. Price is deliberately not asserted (cents vs dollars differ by integration).
/// </para>
/// </summary>
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
