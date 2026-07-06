using System.Net;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

/// <summary>
/// Encodes the middleware's "never leak raw exception text" contract as an executable check: every failure
/// response must be a clean JSON error body — no stack traces, exception type names, or library internals.
/// This is a <b>safety-net</b> test: it passes on BOTH integrations today (Direct via its hand-written
/// <c>MaxioErrorReader</c> sanitizing, Plugin via its <c>ExceptionMiddleware</c> curated messages). Only the
/// provider-error paths are exercised here — all already backed by the mock, so no mock change is needed.
/// (The transport-failure path — provider completely down — is out of scope for this batch; it needs the
/// Plugin transport-error fix and connection-drop simulation.)
/// </summary>
[Trait(MaxioTraits.Category, MaxioTraits.CategorySafetyNet)]
[Trait(MaxioTraits.Api, MaxioTraits.ReadSubscription)]
[Trait(MaxioTraits.Api, MaxioTraits.HoldSubscription)]
[Trait(MaxioTraits.Api, MaxioTraits.CreateSubscription)]
[Trait(MaxioTraits.Api, MaxioTraits.CancelSubscription)]
[Trait(MaxioTraits.Api, MaxioTraits.MigrateSubscription)]
public class ErrorHygieneTests
{
    // Substrings that would betray an internal detail leaking into a response body.
    private static readonly string[] ForbiddenSubstrings =
    {
        "System.", "   at ", "Exception", "MaxioAdvancedBilling", "HttpRequestException", "Polly", "StackTrace"
    };

    [Theory]
    [InlineData("read-unknown-subscription")]
    [InlineData("pause-on-hold-subscription")]
    [InlineData("create-unknown-product")]
    [InlineData("cancel-already-canceled")]
    [InlineData("migrate-unknown-product")]
    public async Task Error_responses_never_leak_internal_details(string scenario)
    {
        var intent = $"Error hygiene: {scenario} response never leaks internal details";
        using var client = new ApiClient();

        var response = await SendFailingRequest(client, scenario);

        Expect.StatusInRange(response, 400, 500, intent, "a 4xx client-error status");

        Expect.ContentType(response, "application/json", intent);

        foreach (var forbidden in ForbiddenSubstrings)
        {
            Expect.NoLeak(response, forbidden, intent);
        }
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

        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown error-hygiene scenario.")
    };
}
