using Xunit;
using Xunit.Abstractions;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// Robustness suite: when Maxio returns a genuine server-side fault (a persistent 5xx, or a 429 rate limit),
/// the integration's infrastructure layer must surface it as a clean <b>server error</b> (5xx) — never a
/// crash, a hang, or a mislabelled client error — with a body that leaks no internals. Contract, independent
/// of either integration's response shape:
/// <list type="bullet">
///   <item>a Maxio 5xx → a 5xx (a provider server-fault is NOT the caller's fault);</item>
///   <item>the error body leaks no framework/exception/parser internals
///   (<see cref="Expect.NoInternalLeak"/>).</item>
/// </list>
///
/// <para>Faults are <b>persistent</b> (every attempt fails), so they survive the client's idempotent-GET
/// retries. The genuine-5xx GET cases here deliberately trip a real integration's shared circuit breaker
/// (e.g. Direct's Polly pipeline), which is why this whole collection is ordered to run <b>last</b> (see
/// <c>AssemblyInfo.cs</c>) — so the open-breaker window never bleeds into a green test.</para>
///
/// <para>Malformed / empty-body responses (which exercise the client's deserialization path, not its
/// status handling) live in <see cref="MalformedResponseTests"/> instead — that suite must run with the
/// breaker CLOSED, so it is deliberately NOT part of this ordered-last collection.</para>
///
/// <para>Status is asserted deterministically (<see cref="Expect.ServerError"/> / <see cref="Expect.NotSuccess"/>)
/// plus the mechanical leak sweep; no AI body check — a faulty upstream has no meaningful payload to verify
/// beyond "a clean, non-leaking error", and the AI judgment on such bodies proved non-deterministic.</para>
/// </summary>
[Trait(MaxioTraits.Category, MaxioTraits.CategorySafetyNet)]
[Trait(MaxioTraits.Api, MaxioTraits.ReadSubscription)]
[Trait(MaxioTraits.Api, MaxioTraits.CreateSubscription)]
[Trait(MaxioTraits.Api, MaxioTraits.RecordUsage)]
[Trait(MaxioTraits.Api, MaxioTraits.CancelSubscription)]
[Trait(MaxioTraits.Api, MaxioTraits.LookupCustomer)]
public class ServerFaultTests : BlackBoxTest
{
    public ServerFaultTests(ITestOutputHelper output) : base(output) { }

    // A valid create-subscription body carrying the given product handle (all three customer identifiers are
    // present so either integration resolves the known customer; see CreateSubscriptionTests).
    private static object CreateBody(string productHandle) => new
    {
        subscription = new
        {
            customer_id = long.Parse(TestSettings.KnownCustomerId),
            customer_reference = TestSettings.KnownCustomerReference,
            customer_email = "known@example.com",
            product_handle = productHandle
        }
    };

    [SkippableFact]
    public Task Read_subscription_upstream_500_surfaces_a_server_error() =>
        AssertServerError(
            "Read a subscription when Maxio returns a persistent 500",
            c => c.GetAsync(TestSettings.SubscriptionPath(TestSettings.ServerError500SubscriptionId)));

    [SkippableFact]
    public Task Read_subscription_upstream_503_surfaces_a_server_error() =>
        AssertServerError(
            "Read a subscription when Maxio returns a persistent 503",
            c => c.GetAsync(TestSettings.SubscriptionPath(TestSettings.ServerError503SubscriptionId)));

    [SkippableFact]
    public async Task Read_subscription_upstream_429_surfaces_an_error()
    {
        const string intent = "Read a subscription when Maxio returns a persistent 429 rate limit";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.RateLimited429SubscriptionId));

        // A rate limit may be surfaced as-is (429, a 4xx) or translated to service-unavailable (5xx). Either is
        // acceptable — gate on any non-2xx error, and require the body stays clean.
        Expect.NotSuccess(response, intent);
        Expect.NoInternalLeak(response, intent);
    }

    [SkippableFact]
    public Task Create_subscription_upstream_500_surfaces_a_server_error() =>
        AssertServerError(
            "Create a subscription when Maxio returns a 500",
            c => c.PostAsync(TestSettings.SubscriptionsPath, CreateBody(TestSettings.ServerError500ProductHandle)));

    [SkippableFact]
    public Task Record_usage_upstream_500_surfaces_a_server_error() =>
        AssertServerError(
            "Record usage when Maxio returns a 500",
            c => c.PostAsync(
                TestSettings.RecordUsagePath(TestSettings.ServerError500SubscriptionId),
                new { usage = new { quantity = 1m, memo = "fault test" } }));

    [SkippableFact]
    public Task Cancel_subscription_upstream_503_surfaces_a_server_error() =>
        AssertServerError(
            "Cancel a subscription when Maxio returns a 503",
            c => c.DeleteAsync(
                TestSettings.SubscriptionPath(TestSettings.ServerError503SubscriptionId),
                new { subscription = new { cancellation_message = "fault test" }, timing = "Immediate" }));

    [SkippableFact]
    public Task Find_or_create_customer_upstream_500_surfaces_a_server_error() =>
        AssertServerError(
            "Find-or-create a customer when the Maxio lookup returns a persistent 500",
            c => c.PostAsync(TestSettings.CustomersPath, LookupFaultBody(TestSettings.NewServerError500Reference())));

    // The fault prefix must reach the mock's lookup whether the integration keys the lookup on the `reference`
    // field or on the email — so both carry it. The email is a syntactically valid address that still starts
    // with the fault prefix (e.g. "fault500_ab@example.com"), so an integration that validates email format
    // accepts it and the fault still triggers on the mock's StartsWith check.
    private static object LookupFaultBody(string reference) => new
    {
        customer = new
        {
            reference,
            email = $"{reference}@example.com",
            first_name = "Fault",
            last_name = "Test"
        }
    };

    private static async Task AssertServerError(string intent, Func<ApiClient, Task<ApiResponse>> act)
    {
        using var client = new ApiClient();

        var response = await act(client);

        // A faulty upstream must surface as a clean 5xx server error with no internal leak. Status + hygiene are
        // both deterministic; no AI body check (see class summary).
        Expect.ServerError(response, intent);
        Expect.NoInternalLeak(response, intent);
    }
}
