using System.Net;
using MaxioApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioApiTests.Tests;

/// <summary>
/// Exercises the metered-component endpoint (<c>GET /api/maxio/metered-component</c>), verifying it returns
/// the configured metered component.
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
