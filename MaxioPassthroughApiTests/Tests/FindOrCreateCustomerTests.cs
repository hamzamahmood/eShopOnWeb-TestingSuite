using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// Find-or-create customer — MaxioBillingController's composite endpoint (POST /api/maxio/customers), same
/// route + request shape on both integrations (readCustomerByReference then, if absent, createCustomer).
/// Response shapes differ (Direct's <c>EnsureCustomer</c> returns a bare provider customer id number;
/// Plugin's <c>FindOrCreateCustomer</c> returns <c>{"customerId": "..."}</c>), so only the resolved id
/// VALUE is compared — see <see cref="TestJson.GetCustomerId"/>. Both return 200 OK.
/// </summary>
public class FindOrCreateCustomerTests
{
    [Fact]
    public async Task Fresh_reference_creates_a_customer_and_is_idempotent_on_repeat_calls()
    {
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
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstId = TestJson.GetCustomerId(JsonDocument.Parse(first.Body).RootElement);
        Assert.False(string.IsNullOrWhiteSpace(firstId));

        // Same fresh reference again: find-or-create must be idempotent — it resolves to the SAME
        // provider customer id rather than creating a duplicate (IBillingClient.FindOrCreateCustomerAsync's
        // documented AC-03 guarantee).
        var second = await client.PostAsync(TestSettings.CustomersPath, body);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondId = TestJson.GetCustomerId(JsonDocument.Parse(second.Body).RootElement);

        Assert.Equal(firstId, secondId);
    }

    [Fact]
    public async Task Blank_email_is_rejected_before_reaching_the_billing_provider()
    {
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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
