using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// List a customer's subscriptions — <c>MaxioBillingController</c> endpoint
/// <c>GET /api/maxio/customers/{customerId}/subscriptions</c> (same route on both integrations).
///
/// <para>
/// The response is a FLATTENED, provider-agnostic DTO array — not Maxio's raw <c>{ "subscription": {...} }</c>
/// envelope. The two integrations expose different shapes (Direct's <c>BillingSubscription</c> has
/// <c>providerSubscriptionId</c> + <c>providerCustomerId</c> and state <c>"active"</c>; Plugin's
/// <c>SubscriptionResponse</c> has <c>subscriptionId</c> + <c>customerReference</c> + <c>productName</c> and
/// state <c>"Active"</c>). This test asserts only the fields common to BOTH: <c>productHandle</c>,
/// <c>state</c> (compared case-insensitively), and the presence of <c>nextAssessmentAt</c>.
/// </para>
///
/// <para>
/// Failure: an unknown (well-formed numeric) customer id no longer passes Maxio's raw 404 through — the
/// provider 404 is surfaced by the client as a <c>BillingProviderException</c> and remapped by the shared
/// <c>ExceptionMiddleware</c>. The resulting status DIFFERS by integration (Direct → 422 UnprocessableEntity;
/// Plugin → 502 BadGateway), so the assertion accepts either — the common, meaningful guarantee is that an
/// unknown id yields an error status, never a 200 with data.
/// </para>
/// </summary>
public class SubscriptionTests
{
    [Fact]
    public async Task Known_customer_returns_the_subscriptions_array_with_common_fields()
    {
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.CustomerSubscriptionsPath(TestSettings.KnownCustomerId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.ContentType);

        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;

        // Flattened DTO shape: a bare JSON array of subscription objects (no Maxio "subscription" envelope).
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(1, root.GetArrayLength());

        var subscription = root[0];
        Assert.Equal("gold", subscription.GetProperty("productHandle").GetString());

        // State casing differs by integration (Direct "active" vs Plugin "Active"); compare case-insensitively.
        Assert.Equal("active", subscription.GetProperty("state").GetString(), ignoreCase: true);

        // The next-assessment timestamp is present and non-null on both shapes.
        Assert.Equal(JsonValueKind.String, subscription.GetProperty("nextAssessmentAt").ValueKind);
    }

    [Fact]
    public async Task Unknown_customer_yields_an_error_status()
    {
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.CustomerSubscriptionsPath(TestSettings.UnknownCustomerId));

        // No raw-404 passthrough anymore: the client wraps Maxio's 404 as a provider error and the
        // middleware remaps it — to 422 on Direct, 502 on Plugin. Either proves it was NOT a 200 with data.
        Assert.True(
            response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadGateway,
            $"Expected 422 (Direct) or 502 (Plugin) for an unknown customer id, but got {(int)response.StatusCode}. Body: {response.Body}");
    }
}
