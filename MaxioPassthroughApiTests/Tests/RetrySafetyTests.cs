using System.Net;
using Xunit;
using Xunit.Abstractions;

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
public class RetrySafetyTests : BlackBoxTest
{
    public RetrySafetyTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Rate_limited_lookup_recovers()
    {
        const string intent = "Recover from a 429 rate limit on the find-or-create lookup";
        using var client = new ApiClient();
        // The special reference token must reach the mock's customer-lookup regardless of integration: Direct
        // looks up by the `reference` field, the Plugin by the `email`. Carry the token in BOTH so the
        // ratelimit_/retry_ behavior fires on either.
        var reference = TestSettings.NewRateLimitReference();
        var body = new
        {
            customer = new
            {
                reference,
                email = $"{reference}@example.com",
                first_name = "Rate",
                last_name = "Limited"
            }
        };

        var response = await client.PostAsync(TestSettings.CustomersPath, body);

        // The 200 alone proves the retry pipeline recovered; the returned customer id is incidental, so we
        // don't parse it — keeping this test free of any key-dependent payload read.
        Expect.Status(response, HttpStatusCode.OK, intent);
    }

    [SkippableFact]
    public async Task Transient_503_lookup_recovers()
    {
        const string intent = "Recover from a transient 503 on the find-or-create lookup";
        using var client = new ApiClient();
        // Token carried in both fields (see the rate-limit test above): Direct looks up by reference, Plugin by email.
        var reference = TestSettings.NewTransient5xxReference();
        var body = new
        {
            customer = new
            {
                reference,
                email = $"{reference}@example.com",
                first_name = "Transient",
                last_name = "Recovered"
            }
        };

        var response = await client.PostAsync(TestSettings.CustomersPath, body);

        // The 200 alone proves the retry pipeline recovered; the returned customer id is incidental, so we
        // don't parse it — keeping this test free of any key-dependent payload read.
        Expect.Status(response, HttpStatusCode.OK, intent);
    }
}
