using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// Tests that demonstrate where the <b>Plugin</b> (APIMatic-generated SDK) integration is more robust than
/// the <b>Direct</b> (hand-rolled HTTP) integration for the same <c>MaxioBillingController</c> endpoints.
///
/// <para>
/// UNLIKE the rest of this suite — which asserts only the common subset of behavior so it stays green against
/// either integration — every test here asserts the <i>superior</i> outcome. That means each test is designed
/// to <b>PASS when <c>PUBLICAPI_BASEURL</c> points at the Plugin PublicApi and FAIL when it points at the
/// Direct one</b>. The failure is the point: it pins the exact behavior the Direct integration lacks. Each
/// test's comment records what Direct does instead (verified against both <c>MaxioBillingClient</c>s and their
/// <c>ExceptionMiddleware</c>s).
/// </para>
///
/// <para>
/// Two mock behaviors back these scenarios (see MaxioMockServer/Program.cs): a <c>race_</c>-prefixed customer
/// reference (<see cref="TestSettings.NewRaceReference"/>) and the <see cref="TestSettings.PaymentRequiredProductHandle"/>
/// product handle. Case 1 (missing subscription) needs no mock change.
/// </para>
/// </summary>
[Trait(MaxioTraits.Category, MaxioTraits.CategoryPluginAdvantage)]
public class PluginAdvantageTests : BlackBoxTest
{
    public PluginAdvantageTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// A missing subscription should yield a REST-correct <c>404 Not Found</c>. The Plugin's
    /// <c>ReadSubscriptionAsync</c> maps Maxio's 404 to <c>SubscriptionNotFoundException</c> → 404. The Direct
    /// client has no not-found special case: any 4xx from Maxio becomes a generic <c>BillingProviderException</c>
    /// which its <c>ExceptionMiddleware</c> maps to <c>422 Unprocessable Entity</c> — so this FAILS on Direct.
    /// </summary>
    [Trait(MaxioTraits.Api, MaxioTraits.ReadSubscription)]
    [SkippableFact]
    public async Task Missing_subscription_returns_404_not_found()
    {
        const string intent = "Read a missing subscription (REST-correct 404 vs Direct's 422)";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.UnknownSubscriptionId));

        Expect.Status(response, HttpStatusCode.NotFound, intent);
    }

    /// <summary>
    /// Find-or-create must remain idempotent even when a customer with the same reference is created
    /// concurrently between the initial lookup and the create. The mock reproduces the race for a
    /// <c>race_</c> reference (lookup 404 → create 422 "already taken" → re-lookup 200). The Plugin's
    /// <c>FindOrCreateCustomerAsync</c> catches the create conflict and re-reads, returning the existing id
    /// (<c>200 OK</c>). The Direct client's <c>EnsureCustomerAsync</c> has no such recovery — the create
    /// conflict surfaces as an error status — so this FAILS on Direct.
    /// </summary>
    [Trait(MaxioTraits.Api, MaxioTraits.LookupCustomer)]
    [Trait(MaxioTraits.Api, MaxioTraits.CreateCustomer)]
    [SkippableFact]
    public async Task Find_or_create_customer_recovers_from_a_concurrent_create_race()
    {
        const string intent = "Recover find-or-create from a concurrent create race";
        using var client = new ApiClient();
        var body = new
        {
            customer = new
            {
                reference = TestSettings.NewRaceReference(),
                email = "race.recovered@example.com",
                first_name = "Race",
                last_name = "Recovered"
            }
        };

        var response = await client.PostAsync(TestSettings.CustomersPath, body);

        // Recovery is fully proven by the 200 (find-or-create resolved despite the concurrent-create race).
        // The returned id is incidental here, so we don't read it — no key-dependent payload parsing.
        Expect.Status(response, HttpStatusCode.OK, intent);
    }

    /// <summary>
    /// Every flavor of payment/card validation failure on create-subscription should surface as the same
    /// typed, user-actionable error. Both integrations return <c>422</c> here, but only the Plugin classifies
    /// the card/payment messages (via its keyword matcher) as a <c>PaymentVerificationRequiredException</c>,
    /// whose distinctive message ("Additional payment information is required…") it writes into the response
    /// body — regardless of which underlying provider message the mock returns. The Direct client returns
    /// only Maxio's raw provider messages, so the Plugin-specific phrase is absent — this FAILS on Direct on
    /// the body assertion for every case. The mock backs one handle per case (see its
    /// <c>paymentFailureHandles</c> map).
    /// </summary>
    [Trait(MaxioTraits.Api, MaxioTraits.CreateSubscription)]
    [SkippableTheory]
    [MemberData(nameof(PaymentRequiredProductHandles))]
    public async Task Payment_failure_surfaces_a_typed_payment_verification_error(string productHandle)
    {
        var intent = $"Create a subscription with payment-failure handle '{productHandle}' " +
                      "(typed payment-verification error)";
        using var client = new ApiClient();
        var body = new
        {
            subscription = new
            {
                customer_id = long.Parse(TestSettings.KnownCustomerId),
                product_handle = productHandle
            }
        };

        var response = await client.PostAsync(TestSettings.SubscriptionsPath, body);

        Expect.Status(response, HttpStatusCode.UnprocessableEntity, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response body indicates that additional payment information is required to activate or " +
            "complete the subscription. A message such as \"Additional payment information is required\" " +
            "satisfies this rule."
        ]);
        Expect.AiPassed(report, intent);
    }

    public static IEnumerable<object[]> PaymentRequiredProductHandles() =>
        TestSettings.PaymentRequiredProductHandles.Select(handle => new object[] { handle });
}
