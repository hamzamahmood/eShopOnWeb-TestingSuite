using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.LookupCustomer)]
[Trait(MaxioTraits.Api, MaxioTraits.CreateCustomer)]
public class FindOrCreateCustomerTests : BlackBoxTest
{
    public FindOrCreateCustomerTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Fresh_reference_creates_a_customer_and_is_idempotent_on_repeat_calls()
    {
        const string intent = "Create a customer for a fresh reference, then repeat the call idempotently";
        using var client = new ApiClient();
        var reference = TestSettings.NewCustomerReference();
        var body = new
        {
            customer = new
            {
                reference,
                email = "fresh.customer@example.com",
                first_name = "Fresh",
                last_name = "Customer"
            }
        };

        var first = await client.PostAsync(TestSettings.CustomersPath, body);
        Expect.Status(first, HttpStatusCode.OK, intent);

        // Same fresh reference again: find-or-create must be idempotent — it resolves to the SAME provider
        // customer id rather than creating a duplicate (IBillingClient.FindOrCreateCustomerAsync's documented
        // AC-03 guarantee). Status 200 alone can't prove that, so we compare the customer id ACROSS both
        // responses — via the AI verifier, so the check is robust to whatever the id field is named.
        var second = await client.PostAsync(TestSettings.CustomersPath, body);
        Expect.Status(second, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(
            $"FIRST RESPONSE:\n{first.Body}\n\nSECOND RESPONSE (same reference, repeated call):\n{second.Body}",
            new[]
            {
                "Each of the two responses contains a non-blank customer identifier.",
                "Both responses identify the SAME customer: the customer id value is identical across the two " +
                "responses (idempotent — no duplicate customer was created).",
            });
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Known_reference_resolves_to_the_existing_customer_idempotently()
    {
        const string intent = "Find an already-existing customer by a known reference (no duplicate created)";
        using var client = new ApiClient();
        // An integration may look the customer up by the `reference` field or by the email (used as the
        // provider reference). The mock recognizes BOTH the known reference and the known email as resolving to
        // the same customer, and the email is a valid address (an integration that validates email format
        // accepts it), so the find succeeds regardless of which field the integration keys on.
        var body = new
        {
            customer = new
            {
                reference = TestSettings.KnownCustomerReference,
                email = TestSettings.KnownCustomerEmail,
                first_name = "John",
                last_name = "Doe"
            }
        };

        var response = await client.PostAsync(TestSettings.CustomersPath, body);
        Expect.Status(response, HttpStatusCode.OK, intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response contains a non-blank customer identifier (the existing customer was returned)."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Blank_email_is_rejected_before_reaching_the_billing_provider()
    {
        const string intent = "Reject a blank email before reaching the billing provider";
        using var client = new ApiClient();
        var body = new
        {
            customer = new
            {
                reference = TestSettings.NewCustomerReference(),
                email = "",
                first_name = "Fresh",
                last_name = "Customer"
            }
        };

        var response = await client.PostAsync(TestSettings.CustomersPath, body);

        // The Plugin surfaces the rejection as 502; the Direct integration preserves the Maxio status (a 4xx)
        // and fails this assertion by design.
        Expect.Status(response, HttpStatusCode.BadGateway, intent); // 502 expected (Plugin)

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the customer could not be created / was rejected (any wording)."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Missing_customer_envelope_is_rejected()
    {
        const string intent = "Reject a create-customer request whose `customer` envelope is absent";
        using var client = new ApiClient();
        var body = new { };

        var response = await client.PostAsync(TestSettings.CustomersPath, body);

        // The Plugin surfaces the rejection as 502; the Direct integration returns a 4xx and fails by design.
        Expect.Status(response, HttpStatusCode.BadGateway, intent); // 502 expected (Plugin)

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the customer could not be created / was rejected (any wording)."
        ]);
        Expect.AiPassed(report, intent);
    }

    [SkippableFact]
    public async Task Provider_object_map_error_shape_is_surfaced_cleanly()
    {
        const string intent = "Surface a provider object-map error ({errors:{customer:…}}) as a clean client error";
        using var client = new ApiClient();
        // A reference whose create the mock rejects with the OBJECT-MAP error shape
        // ({ "errors": { "customer": "…" } }) — the alternate form in the spec's Customer-Error-Response oneOf.
        // The lookup misses first (unknown reference), so the controller proceeds to create, which is rejected.
        // Verifies the integration's error reader handles this shape (not just the {errors:[…]} array form) and
        // the middleware still emits a clean, bodied client error rather than crashing or leaking internals.
        var body = new
        {
            customer = new
            {
                reference = TestSettings.NewObjectMapErrorReference(),
                email = "objmap.error@example.com",
                first_name = "Obj",
                last_name = "Map"
            }
        };

        var response = await client.PostAsync(TestSettings.CustomersPath, body);

        Expect.Status(response, HttpStatusCode.BadGateway, intent); // 502 — Plugin surfaces provider errors as 502; Direct returns 4xx and fails here by design

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response communicates that the customer could not be created — for example the reference was " +
            "already taken / a validation problem. Any wording conveying the create was rejected satisfies this rule."
        ]);
        Expect.AiPassed(report, intent);
    }
}
