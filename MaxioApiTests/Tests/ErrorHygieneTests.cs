using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace MaxioApiTests.Tests;

/// <summary>
/// Verifies that failure responses are always clean JSON error bodies with no leaked internal details.
/// </summary>
[Trait(MaxioTraits.Category, MaxioTraits.CategorySafetyNet)]
[Trait(MaxioTraits.Api, MaxioTraits.ReadSubscription)]
[Trait(MaxioTraits.Api, MaxioTraits.HoldSubscription)]
[Trait(MaxioTraits.Api, MaxioTraits.CreateSubscription)]
[Trait(MaxioTraits.Api, MaxioTraits.CancelSubscription)]
[Trait(MaxioTraits.Api, MaxioTraits.MigrateSubscription)]
public class ErrorHygieneTests : BlackBoxTest
{
    public ErrorHygieneTests(ITestOutputHelper output) : base(output) { }

    [SkippableTheory]
    [InlineData("read-unknown-subscription")]
    public async Task Error_responses_never_leak_internal_details(string scenario)
    {
        var intent = $"Error hygiene: {scenario} response never leaks internal details";
        using var client = new ApiClient();

        var response = await SendFailingRequest(client, scenario);

        Expect.NotSuccess(response, intent);

        Expect.ContentType(response, "application/json", intent);
        Expect.NoInternalLeak(response, intent);
    }

    private static Task<ApiResponse> SendFailingRequest(ApiClient client, string scenario) => scenario switch
    {
        "read-unknown-subscription" =>
            client.GetAsync(TestSettings.SubscriptionPath(TestSettings.UnknownSubscriptionId)),

        "pause-on-hold-subscription" =>
            client.PostAsync(TestSettings.PauseSubscriptionPath(TestSettings.KnownOnHoldSubscriptionId)),

        "create-unknown-product" =>
            client.PostAsync(TestSettings.SubscriptionsPath, new
            {
                subscription = new
                {
                    customer_id = long.Parse(TestSettings.KnownCustomerId),
                    product_handle = TestSettings.UnknownProductHandle
                }
            }),

        "cancel-already-canceled" =>
            client.DeleteAsync(TestSettings.SubscriptionPath(TestSettings.KnownCanceledSubscriptionId), new
            {
                subscription = new { cancellation_message = "No longer needed" },
                timing = "Immediate"
            }),

        "migrate-unknown-product" =>
            client.PostAsync(TestSettings.MigrationsPath(TestSettings.KnownActiveSubscriptionId), new
            {
                migration = new { product_handle = TestSettings.UnknownProductHandle },
                timing = "Immediate"
            }),

        "create-customer-object-map-error" =>
            client.PostAsync(TestSettings.CustomersPath, new
            {
                customer = new
                {
                    reference = TestSettings.NewObjectMapErrorReference(),
                    email = "objmap.hygiene@example.com",
                    first_name = "Obj",
                    last_name = "Map"
                }
            }),

        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown error-hygiene scenario.")
    };
}
