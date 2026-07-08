using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// Black-box analogue of the integrations' former white-box "resilient retry recovery" unit tests. The mock
/// interrupts the find-or-create endpoint's internal customer-lookup GET with a simulated transport-level
/// connection break on the first attempt (see the mock's connbreak_ branch); the PublicApi's retry pipeline
/// retries the idempotent GET and the caller's request still succeeds. Safety-net test: passes on BOTH
/// integrations (Direct via its Polly resilience pipeline, Plugin via the SDK's default RetryOptions). At the
/// black-box layer we can only observe recovery (a final 200 + customer id), not the in-process attempt/break
/// counts the old unit test asserted.
/// </summary>
[Trait(MaxioTraits.Category, MaxioTraits.CategorySafetyNet)]
[Trait(MaxioTraits.Api, MaxioTraits.LookupCustomer)]
[Trait(MaxioTraits.Api, MaxioTraits.CreateCustomer)]
public class ResilientRetryRecoveryTests : BlackBoxTest
{
    public ResilientRetryRecoveryTests(ITestOutputHelper output) : base(output) { }

    private const int CallCount = 12;

    [SkippableFact]
    public async Task Recovers_from_intermittent_connection_breaks_across_many_calls()
    {
        using var client = new ApiClient();

        for (var i = 0; i < CallCount; i++)
        {
            var intent = $"Recover from a connection break on find-or-create call {i + 1}/{CallCount}";
            // Token carried in both `reference` and `email` so the mock's connbreak_ branch fires whether the
            // integration looks the customer up by reference (Direct) or by email (Plugin).
            var reference = TestSettings.NewConnectionInterruptReference();
            var body = new
            {
                customer = new
                {
                    reference,
                    email = $"{reference}@example.com",
                    first_name = "Conn",
                    last_name = "Break"
                }
            };

            var response = await client.PostAsync(TestSettings.CustomersPath, body);

            // Recovery is observable purely from the final 200; the customer id is incidental, so we skip
            // parsing it (no key-dependent payload read, and no per-iteration model call).
            Expect.Status(response, HttpStatusCode.OK, intent);
        }
    }
}
