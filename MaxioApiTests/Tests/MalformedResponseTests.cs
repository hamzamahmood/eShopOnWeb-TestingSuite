using Xunit;
using Xunit.Abstractions;

namespace MaxioApiTests.Tests;

/// <summary>
/// Robustness suite: when Maxio returns a well-formed HTTP <b>200</b> whose body the client cannot parse
/// (truncated/malformed JSON, or an empty body), the integration must fail gracefully — surface a clean
/// <b>server error</b> (5xx) and leak no internals — rather than crash, hang, or echo the raw deserializer
/// diagnostic (byte position / JSON path / "invalid token") back to the caller.
///
/// <para>These 200-status responses do NOT count as pipeline failures, so they neither trip nor are masked by
/// a circuit breaker on their own — but a breaker left open by another suite WOULD mask them (short-circuit
/// before the body is ever fetched). So, unlike <see cref="ServerFaultTests"/>, this collection is deliberately
/// NOT ordered last: it runs in the normal window with the breaker closed, so it genuinely exercises the
/// deserialization path.</para>
///
/// <para>Status + hygiene are asserted deterministically; no AI body check (a body the client failed to parse
/// has no payload to verify — the only contract is "clean 5xx, no leaked parser internals").</para>
/// </summary>
[Trait(MaxioTraits.Category, MaxioTraits.CategorySafetyNet)]
[Trait(MaxioTraits.Api, MaxioTraits.ReadSubscription)]
[Trait(MaxioTraits.Api, MaxioTraits.CreateSubscription)]
[Trait(MaxioTraits.Api, MaxioTraits.LookupCustomer)]
public class MalformedResponseTests : BlackBoxTest
{
    public MalformedResponseTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public Task Read_subscription_malformed_body_surfaces_a_clean_server_error() =>
        AssertCleanServerError(
            "Read a subscription when Maxio returns a 200 with a malformed body",
            c => c.GetAsync(TestSettings.SubscriptionPath(TestSettings.MalformedBodySubscriptionId)));

    [SkippableFact]
    public Task Read_subscription_empty_body_surfaces_a_clean_server_error() =>
        AssertCleanServerError(
            "Read a subscription when Maxio returns a 200 with an empty body",
            c => c.GetAsync(TestSettings.SubscriptionPath(TestSettings.EmptyBodySubscriptionId)));

    [SkippableFact]
    public Task Create_subscription_malformed_body_surfaces_a_clean_server_error() =>
        AssertCleanServerError(
            "Create a subscription when Maxio returns a 200 with a malformed body",
            c => c.PostAsync(TestSettings.SubscriptionsPath, new
            {
                subscription = new
                {
                    customer_id = long.Parse(TestSettings.KnownCustomerId),
                    customer_reference = TestSettings.KnownCustomerReference,
                    customer_email = "known@example.com",
                    product_handle = TestSettings.MalformedResponseProductHandle
                }
            }));

    [SkippableFact]
    public Task Find_or_create_customer_malformed_body_surfaces_a_clean_server_error()
    {
        var reference = TestSettings.NewMalformedBodyReference();
        return AssertCleanServerError(
            "Find-or-create a customer when the Maxio lookup returns a 200 with a malformed body",
            c => c.PostAsync(TestSettings.CustomersPath, new
            {
                customer = new
                {
                    reference,
                    email = $"{reference}@example.com",
                    first_name = "Malformed",
                    last_name = "Test"
                }
            }));
    }

    private static async Task AssertCleanServerError(string intent, Func<ApiClient, Task<ApiResponse>> act)
    {
        using var client = new ApiClient();

        var response = await act(client);

        // An unparseable upstream body must surface as a clean 5xx that leaks no raw parser diagnostics
        // (byte position / JSON path / "invalid token" / "does not contain any JSON tokens").
        Expect.ServerError(response, intent);
        Expect.NoInternalLeak(response, intent);
    }
}
