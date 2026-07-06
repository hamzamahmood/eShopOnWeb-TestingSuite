using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// When the provider answers <c>429</c> (rate limit) or a transient <c>503</c> (brief outage) on a safe read,
/// the integration quietly retries with backoff and the caller's request still succeeds. This is a
/// <b>safety-net</b> test: it passes on BOTH integrations (Direct via its hand-written Polly resilience
/// pipeline, Plugin via the SDK's default <c>RetryOptions</c> — both retry idempotent GETs). The story is the
/// asymmetry: Direct hand-wrote the resilience; the Plugin got it as an SDK default.
///
/// <para>
/// Each test drives the find-or-create endpoint (<c>POST /api/maxio/customers</c>) — whose internal customer
/// <i>lookup GET</i> is the idempotent read the mock fails once then succeeds. A fresh nonce-bearing reference
/// per run keeps the mock's per-reference attempt counter independent of test ordering.
/// </para>
/// </summary>
[Trait(MaxioTraits.Category, MaxioTraits.CategorySafetyNet)]
[Trait(MaxioTraits.Api, MaxioTraits.LookupCustomer)]
[Trait(MaxioTraits.Api, MaxioTraits.CreateCustomer)]
public class RetrySafetyTests
{
    [Fact]
    public async Task Rate_limited_lookup_recovers()
    {
        using var client = new ApiClient();
        var body = new
        {
            customer = new
            {
                reference = TestSettings.NewRateLimitReference(),
                email = "ratelimit.recovered@example.com",
                first_name = "Rate",
                last_name = "Limited"
            }
        };

        var response = await client.PostAsync(TestSettings.CustomersPath, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var customerId = TestJson.GetCustomerId(JsonDocument.Parse(response.Body).RootElement);
        Assert.False(string.IsNullOrWhiteSpace(customerId));
    }

    [Fact]
    public async Task Transient_503_lookup_recovers()
    {
        using var client = new ApiClient();
        var body = new
        {
            customer = new
            {
                reference = TestSettings.NewTransient5xxReference(),
                email = "transient.recovered@example.com",
                first_name = "Transient",
                last_name = "Recovered"
            }
        };

        var response = await client.PostAsync(TestSettings.CustomersPath, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var customerId = TestJson.GetCustomerId(JsonDocument.Parse(response.Body).RootElement);
        Assert.False(string.IsNullOrWhiteSpace(customerId));
    }
}
