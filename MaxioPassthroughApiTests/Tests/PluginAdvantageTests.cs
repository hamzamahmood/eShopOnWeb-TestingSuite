using System.Net;
using System.Text.Json;
using Xunit;

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
public class PluginAdvantageTests
{
    /// <summary>
    /// A missing subscription should yield a REST-correct <c>404 Not Found</c>. The Plugin's
    /// <c>ReadSubscriptionAsync</c> maps Maxio's 404 to <c>SubscriptionNotFoundException</c> → 404. The Direct
    /// client has no not-found special case: any 4xx from Maxio becomes a generic <c>BillingProviderException</c>
    /// which its <c>ExceptionMiddleware</c> maps to <c>422 Unprocessable Entity</c> — so this FAILS on Direct.
    /// </summary>
    [Fact]
    public async Task Missing_subscription_returns_404_not_found()
    {
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.UnknownSubscriptionId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Find-or-create must remain idempotent even when a customer with the same reference is created
    /// concurrently between the initial lookup and the create. The mock reproduces the race for a
    /// <c>race_</c> reference (lookup 404 → create 422 "already taken" → re-lookup 200). The Plugin's
    /// <c>FindOrCreateCustomerAsync</c> catches the create conflict and re-reads, returning the existing id
    /// (<c>200 OK</c>). The Direct client's <c>EnsureCustomerAsync</c> has no such recovery — the create
    /// conflict surfaces as an error status — so this FAILS on Direct.
    /// </summary>
    [Fact]
    public async Task Find_or_create_customer_recovers_from_a_concurrent_create_race()
    {
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var customerId = TestJson.GetCustomerId(JsonDocument.Parse(response.Body).RootElement);
        Assert.False(string.IsNullOrWhiteSpace(customerId));
    }

    /// <summary>
    /// A payment/card validation failure on create-subscription should surface as a typed, user-actionable
    /// error. Both integrations return <c>422</c> here, but only the Plugin classifies the card/payment
    /// messages as a <c>PaymentVerificationRequiredException</c>, whose distinctive message
    /// ("Additional payment information is required…") it writes into the response body. The Direct client
    /// returns only Maxio's raw provider messages, so the Plugin-specific phrase is absent — this FAILS on
    /// Direct on the body assertion.
    /// </summary>
    [Fact]
    public async Task Payment_failure_surfaces_a_typed_payment_verification_error()
    {
        using var client = new ApiClient();
        var body = new
        {
            subscription = new
            {
                customer_id = long.Parse(TestSettings.KnownCustomerId),
                product_handle = TestSettings.PaymentRequiredProductHandle
            }
        };

        var response = await client.PostAsync(TestSettings.SubscriptionsPath, body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Additional payment information is required", response.Body);
    }
}
