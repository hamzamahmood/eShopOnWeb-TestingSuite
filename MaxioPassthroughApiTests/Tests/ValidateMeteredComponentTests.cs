using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// The metered-component guard endpoint (<c>GET /api/maxio/metered-component</c>) as exposed by the run_3
/// integrations: both now serve it and return <b>200 + data</b> describing the configured metered component
/// (Direct reflects the provider's component; the Plugin returns the configured id/handle after validating it).
/// The response shapes differ, so the body is judged by meaning via the LLM verifier.
///
/// <para>(The older <c>GET /metered-component/verify</c> → 204 form no longer exists; the legacy
/// <c>MeteredComponentTests</c> targeting it auto-skips on both integrations.)</para>
/// </summary>
[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ReadSubscriptionComponent)]
public class ValidateMeteredComponentTests : BlackBoxTest
{
    public ValidateMeteredComponentTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Configured_metered_component_is_validated_and_described()
    {
        const string intent = "Validate/read the configured metered component";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.MeteredComponentReadPath);

        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response confirms the configured metered component is valid — either by describing a metered " +
            "component (handle 'api-calls' and/or id 641814, of a metered kind) or by confirming it is verified " +
            "(e.g. a verified/validated flag set to true). Either form satisfies this rule."
        ]);
        Expect.AiPassed(report, intent);
    }
}
