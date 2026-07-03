using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// Providers add subscription-state enum values without warning. The SDK's enums are open (unknown values
/// deserialize instead of throwing), and the Plugin's <c>MapState</c> routes anything unrecognized to a safe
/// default (<c>Other</c>). The Direct client forwards the raw unknown string straight to API consumers.
///
/// <para>
/// This test asserts the Plugin's behavior, so it PASSES on Plugin and FAILS on Direct by design: for a
/// subscription whose provider state is the unknown value <c>"assessing"</c>, the Plugin returns
/// <c>"Other"</c> (matches) while Direct returns raw <c>"assessing"</c> (fails the <c>StatesEqual</c> check).
/// Backed by the mock's canned subscription <see cref="TestSettings.UnknownStateSubscriptionId"/>.
/// </para>
/// </summary>
public class StateDriftTests
{
    [Fact]
    public async Task Unknown_provider_state_maps_to_a_safe_default()
    {
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.UnknownStateSubscriptionId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        var state = doc.RootElement.GetProperty("state").GetString();
        Assert.True(
            TestJson.StatesEqual("other", state ?? string.Empty),
            $"Expected the unknown provider state to map to a safe default 'Other', got '{state}'.");
    }
}
