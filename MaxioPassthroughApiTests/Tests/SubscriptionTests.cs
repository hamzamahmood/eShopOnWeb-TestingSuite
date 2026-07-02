using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// GET /api/subscription?customerId={id} — proxies Maxio's list-customer-subscriptions.
/// Success returns Maxio's subscription array; an unknown (but well-formed numeric) customer id must pass
/// Maxio's EXACT 404 through. A numeric id is used so both integrations behave identically (the SDK-based
/// Plugin parses the id as a number before the call).
/// </summary>
public class SubscriptionTests
{
    [Fact]
    public async Task Known_customer_returns_the_Maxio_subscriptions_array()
    {
        using var client = new ApiClient();

        var response = await client.GetAsync($"/api/subscription?customerId={TestSettings.KnownCustomerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.ContentType);

        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;

        // Maxio wire shape: an array of { "subscription": { ... } } envelopes.
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(1, root.GetArrayLength());

        var subscription = root[0].GetProperty("subscription");
        Assert.Equal(15100121, subscription.GetProperty("id").GetInt32());
        Assert.Equal("active", subscription.GetProperty("state").GetString());
        Assert.Equal(98765, subscription.GetProperty("customer").GetProperty("id").GetInt32());
        Assert.Equal("gold", subscription.GetProperty("product").GetProperty("handle").GetString());
        Assert.Equal("Gold Plan", subscription.GetProperty("product").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Unknown_customer_passes_through_Maxios_exact_404()
    {
        using var client = new ApiClient();

        var response = await client.GetAsync($"/api/subscription?customerId={TestSettings.UnknownCustomerId}");

        // The exact Maxio status must reach the caller — NOT a remapped 422/502.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var doc = JsonDocument.Parse(response.Body);
        var errors = doc.RootElement.GetProperty("errors");
        Assert.Equal(JsonValueKind.Array, errors.ValueKind);
        Assert.Contains(
            errors.EnumerateArray().Select(e => e.GetString()),
            message => message is not null && message.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }
}
