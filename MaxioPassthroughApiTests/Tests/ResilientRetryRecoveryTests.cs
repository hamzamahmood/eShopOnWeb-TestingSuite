using System.Net;
using System.Text.Json;
using Xunit;

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
public class ResilientRetryRecoveryTests
{
    private const int CallCount = 12;

    [SkippableFact]
    public async Task Recovers_from_intermittent_connection_breaks_across_many_calls()
    {
        using var client = new ApiClient();

        for (var i = 0; i < CallCount; i++)
        {
            var intent = $"Recover from a connection break on find-or-create call {i + 1}/{CallCount}";
            var body = new
            {
                customer = new
                {
                    reference = TestSettings.NewConnectionInterruptReference(),
                    email = $"connbreak.recovered.{i}@example.com",
                    first_name = "Conn",
                    last_name = "Break"
                }
            };

            var response = await client.PostAsync(TestSettings.CustomersPath, body);

            Expect.Status(response, HttpStatusCode.OK, intent);
            var customerId = TestJson.GetCustomerId(JsonDocument.Parse(response.Body).RootElement);
            Expect.NonBlankId(customerId, "customer id", intent);
        }
    }
}
