using System.Net;
using System.Text.Json;
using Xunit;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ListCustomerSubs)]
public class SubscriptionTests
{
    [Fact]
    public async Task Known_customer_returns_the_subscriptions_array_with_common_fields()
    {
        const string intent = "List a known customer's subscriptions with their common fields";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.CustomerSubscriptionsPath(TestSettings.KnownCustomerId));

        Expect.Status(response, HttpStatusCode.OK, intent);
        Expect.ContentType(response, "application/json", intent);

        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;

        // Flattened DTO shape: a bare JSON array of subscription objects (no Maxio "subscription" envelope).
        Expect.Equal(JsonValueKind.Array, root.ValueKind, "response shape", intent);
        Expect.Equal(1, root.GetArrayLength(), "subscription array length", intent);

        var subscription = root[0];
        Expect.Field(subscription, "productHandle", "gold", intent);

        // State casing differs by integration (Direct "active" vs Plugin "Active"); StatesEqual is separator-
        // and case-insensitive.
        Expect.State(subscription, "active", intent);

        // The next-assessment timestamp is present and non-null on both shapes.
        Expect.Equal(JsonValueKind.String, subscription.GetProperty("nextAssessmentAt").ValueKind, "'nextAssessmentAt' field kind", intent);
    }

    [Fact]
    public async Task Unknown_customer_yields_an_error_status()
    {
        const string intent = "List subscriptions for an unknown customer";
        using var client = new ApiClient();

        var response = await client.GetAsync(TestSettings.CustomerSubscriptionsPath(TestSettings.UnknownCustomerId));

        Expect.Status(response, HttpStatusCode.UnprocessableEntity, intent);
    }
}
