using Xunit;
using Xunit.Abstractions;

namespace MaxioApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.FindComponent)]
public class MeteredComponentTests : BlackBoxTest
{
    public MeteredComponentTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Configured_metered_component_verifies_successfully()
    {
        const string intent = "Verify the configured metered component exists";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.MeteredComponentPath);

        // Success status diverges by integration (verify → 204 no body; read → 200 + data), so assert 2xx.
        // There is no common body to verify (one variant returns none), so this is a status-only check.
        Expect.StatusInRange(response, 200, 300, intent, "a 2xx success");
    }
}
