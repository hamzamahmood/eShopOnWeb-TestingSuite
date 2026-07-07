using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.LookupCustomer)]
[Trait(MaxioTraits.Api, MaxioTraits.CreateCustomer)]
public class FindOrCreateCustomerTests
{
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

        // Both controllers require a non-blank email client-side (Direct via an explicit check, Plugin via
        // [Required] + automatic ModelState validation) — this never reaches the mock, so both agree on 400.
        Expect.Status(response, HttpStatusCode.BadRequest, intent);
    }
}
