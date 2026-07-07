using System.Net;
using MaxioPassthroughApiTests.Ai;
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
[Trait(MaxioTraits.Category, MaxioTraits.CategoryPluginAdvantage)]
[Trait(MaxioTraits.Api, MaxioTraits.ReadSubscription)]
public class StateDriftTests
{
    [SkippableFact]
    public async Task Unknown_provider_state_maps_to_a_safe_default()
    {
        const string intent = "Read a subscription whose provider state is unrecognized (safe-default mapping)";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.UnknownStateSubscriptionId));

        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The subscription's state value is exactly the string \"other\" (compare case-insensitively). " +
            "Judge only whether the value equals \"other\"; do not consider whether \"other\" is a healthy or " +
            "recognized lifecycle state."
        ]);
        Expect.AiPassed(report, intent);
    }
}
