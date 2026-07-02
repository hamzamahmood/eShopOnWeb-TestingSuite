using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// Differentiator suite — production-readiness requirements the <b>Plugin</b> integration satisfies and the
/// <b>Direct</b> integration does not. Unlike the five shared tests (which assert behavior common to both),
/// these encode a requirement the Plugin's SDK-based passthrough meets for free but the Direct passthrough —
/// a hand-rolled <c>HttpClient</c> with no resilience pipeline — fails: recovering from a transient upstream
/// failure by retrying the idempotent GET.
///
/// <para>
/// Expected outcome: <b>PASS against the Plugin PublicApi, FAIL against the Direct PublicApi.</b> A failure
/// here run against Direct is the point — it is the measurable gap. These are isolated behind
/// <c>[Trait("Category", "Differentiator")]</c> so the shared suite stays green on both integrations; run
/// this group on its own with <c>dotnet test --filter Category=Differentiator</c>.
/// </para>
///
/// <para>
/// Both behaviors are driven purely by the customer <c>reference</c> the caller supplies, so they exercise
/// the exact same public route (<c>GET /api/customer</c>) as the shared suite — no integration code changes.
/// The mock fails the FIRST request for a fresh, per-run reference and succeeds on a retry; because attempt
/// #1 always fails, the test observing a 200 <i>proves</i> the client retried internally — no need to talk
/// to the mock directly.
/// </para>
/// </summary>
[Trait("Category", "Differentiator")]
public class PluginDifferentiatorTests
{
    /// <summary>
    /// A transient <c>503</c> on an idempotent GET must be recovered by retrying.
    /// <para>Plugin: the SDK retries GET on 5xx (503 is in the default retry set) → 200 + customer.</para>
    /// <para>Direct: the passthrough has no resilience pipeline, issues one request → surfaces the 503.</para>
    /// </summary>
    [Fact]
    public async Task Transient_5xx_is_recovered_by_the_clients_retry_pipeline()
    {
        using var client = new ApiClient();

        // Unique nonce per run: the mock's first attempt for this reference is a transient 503.
        var reference = TestSettings.NewTransient5xxReference();

        var response = await client.GetAsync($"/api/customer?reference={reference}");

        AssertRecoveredToCustomer(
            response,
            transientStatus: "503 Service Unavailable",
            capability: "retry transient 5xx responses");
    }

    /// <summary>
    /// A <c>429 Too Many Requests</c> (Maxio's documented rate-limit response) on an idempotent GET must be
    /// recovered by retrying.
    /// <para>Plugin: the SDK retries GET on 429 (in the default retry set) → 200 + customer.</para>
    /// <para>Direct: the passthrough has no resilience pipeline, issues one request → surfaces the 429.</para>
    /// </summary>
    [Fact]
    public async Task Rate_limit_429_is_recovered_by_the_clients_retry_pipeline()
    {
        using var client = new ApiClient();

        // Unique nonce per run: the mock's first attempt for this reference is a 429 rate limit.
        var reference = TestSettings.NewRateLimitReference();

        var response = await client.GetAsync($"/api/customer?reference={reference}");

        AssertRecoveredToCustomer(
            response,
            transientStatus: "429 Too Many Requests",
            capability: "retry rate-limited (429) responses");
    }

    /// <summary>
    /// Asserts the transient failure was retried and recovered: a 200 carrying the canned customer. The
    /// failure message spells out why the Direct integration falls short when this assertion trips.
    /// </summary>
    private static void AssertRecoveredToCustomer(ApiResponse response, string transientStatus, string capability)
    {
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 after the initial {transientStatus} was retried, but got {(int)response.StatusCode}. " +
            $"The integration under test did not {capability} on an idempotent GET. The Direct passthrough is a " +
            $"hand-rolled HttpClient with no resilience pipeline; the Plugin reuses the SDK client, which retries " +
            $"transient responses. Body: {response.Body}");

        Assert.Equal("application/json", response.ContentType);

        using var doc = JsonDocument.Parse(response.Body);
        var customer = doc.RootElement.GetProperty("customer");
        Assert.Equal(98765, customer.GetProperty("id").GetInt32());
    }
}
