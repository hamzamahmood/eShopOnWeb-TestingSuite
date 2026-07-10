using Xunit;
using Xunit.Abstractions;

namespace MaxioApiTests.Tests;

/// <summary>
/// Verifies that an unparseable or empty success body surfaces as a clean server error with no leaked internals.
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

        Expect.ServerError(response, intent);
        Expect.NoInternalLeak(response, intent);
    }
}
