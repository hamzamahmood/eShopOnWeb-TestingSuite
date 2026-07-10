using Xunit;
using Xunit.Abstractions;

namespace MaxioApiTests.Tests;

/// <summary>
/// When the billing provider returns a server-side fault (a persistent 5xx, or a 429 rate limit), the response
/// must surface as a clean non-2xx error with a body that leaks no internals.
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
        AssertUpstreamFaultSurfacesAsError(
            "Read a subscription when Maxio returns a persistent 500",
            c => c.GetAsync(TestSettings.SubscriptionPath(TestSettings.ServerError500SubscriptionId)));

    [SkippableFact]
    public Task Read_subscription_upstream_503_surfaces_a_server_error() =>
        AssertUpstreamFaultSurfacesAsError(
            "Read a subscription when Maxio returns a persistent 503",
            c => c.GetAsync(TestSettings.SubscriptionPath(TestSettings.ServerError503SubscriptionId)));

    [SkippableFact]
    public async Task Read_subscription_upstream_429_surfaces_an_error()
    {
        const string intent = "Read a subscription when Maxio returns a persistent 429 rate limit";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.SubscriptionPath(TestSettings.RateLimited429SubscriptionId));

        Expect.NotSuccess(response, intent);
        Expect.NoInternalLeak(response, intent);
    }

    [SkippableFact]
    public Task Create_subscription_upstream_500_surfaces_a_server_error() =>
        AssertUpstreamFaultSurfacesAsError(
            "Create a subscription when Maxio returns a 500",
            c => c.PostAsync(TestSettings.SubscriptionsPath, CreateBody(TestSettings.ServerError500ProductHandle)));

    [SkippableFact]
    public Task Record_usage_upstream_500_surfaces_a_server_error() =>
        AssertUpstreamFaultSurfacesAsError(
            "Record usage when Maxio returns a 500",
            c => c.PostAsync(
                TestSettings.RecordUsagePath(TestSettings.ServerError500SubscriptionId),
                new { usage = new { quantity = 1m, memo = "fault test" } }));

    [SkippableFact]
    public Task Cancel_subscription_upstream_503_surfaces_a_server_error() =>
        AssertUpstreamFaultSurfacesAsError(
            "Cancel a subscription when Maxio returns a 503",
            c => c.DeleteAsync(
                TestSettings.SubscriptionPath(TestSettings.ServerError503SubscriptionId),
                new { subscription = new { cancellation_message = "fault test" }, timing = "Immediate" }));

    [SkippableFact]
    public Task Find_or_create_customer_upstream_500_surfaces_a_server_error() =>
        AssertUpstreamFaultSurfacesAsError(
            "Find-or-create a customer when the Maxio lookup returns a persistent 500",
            c => c.PostAsync(TestSettings.CustomersPath, LookupFaultBody(TestSettings.NewServerError500Reference())));

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

    private static async Task AssertUpstreamFaultSurfacesAsError(string intent, Func<ApiClient, Task<ApiResponse>> act)
    {
        using var client = new ApiClient();

        var response = await act(client);

        Expect.NotSuccess(response, intent);
        Expect.NoInternalLeak(response, intent);
    }
}
