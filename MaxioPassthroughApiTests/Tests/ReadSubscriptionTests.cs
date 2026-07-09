using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ReadSubscription)]
public class ReadSubscriptionTests : BlackBoxTest
{
    public ReadSubscriptionTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Known_subscription_returns_its_common_fields()
    {
        const string intent = "Read a known subscription's common fields";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.KnownActiveSubscriptionId));

        Expect.Status(response, HttpStatusCode.OK, intent);

        // This is the canonical "does the flattening preserve the subscription contract" case: it asserts the
        // full set of fields Maxio's spec Subscription example carries. Body verification is AI-judged and
        // matches on MEANING, not exact key names/casing/units: the customer reference and plan handle may be
        // under camelCase or snake_case keys, the price in cents or dollars, and dates in any format — all
        // treated as equivalent. (Fields the integration drops during flattening surface here as failures — a
        // deliberate robustness signal, not a regression.)
        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response contains a non-blank unique subscription identifier.",
            "The subscription's lifecycle state is conveyed as a descriptive text value (e.g. 'active'), not an opaque numeric code, and indicates it is active.",
            $"The subscription is for the product/plan with handle '{TestSettings.KnownProductHandle}'.",
            "The subscription conveys a recurring product/plan price (any units — cents or dollars).",
            $"The subscription belongs to the customer with reference '{TestSettings.KnownCustomerReference}'.",
            "The subscription conveys when the current billing period ends (a date/time, any format)."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Assessing_state_subscription_is_read_and_its_state_surfaced()
    {
        const string intent = "Read a subscription in a plausible-but-unknown provider state (assessing)";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.UnknownStateSubscriptionId));

        // A readable subscription in an unusual state must still return successfully — the integration should
        // surface a state rather than choke on an unrecognized value (Direct forwards the raw string; the
        // Plugin maps it to a safe status plus a rawState).
        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response is a single subscription with a non-blank unique subscription identifier.",
            "The subscription's lifecycle state is conveyed as a descriptive text value (e.g. 'assessing'), not an opaque numeric code."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Unknown_subscription_yields_an_error_status()
    {
        const string intent = "Read an unknown subscription";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.UnknownSubscriptionId));

        // Reading a missing subscription is a caller mistake — it must surface as a 4xx client error (both
        // integrations return a bodied 404; the status is gated loosely per the contract-driven design).
        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client error");

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the subscription was not found / does not exist."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Non_numeric_subscription_id_is_rejected_with_a_client_error()
    {
        const string intent = "Reject a non-numeric subscription id with a client error";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.NonNumericSubscriptionId));

        // Both return a bodied client error: the integration that parses the id itself emits a 400 with a
        // custom message; the one that binds the id as `int` emits ASP.NET's 400 problem+json ("The value
        // 'abc' is not valid."). Either way it is a non-2xx with a body the LLM can check.
        Expect.NotSuccess(response, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the supplied subscription id is not a valid integer / is invalid."
        ]);
        Expect.AiPassed(report, intent);
    }
}
