using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MaxioPassthroughApiTests.Tests;

[Trait(MaxioTraits.Category, MaxioTraits.CategoryEndpoint)]
[Trait(MaxioTraits.Api, MaxioTraits.ListCustomerSubs)]
public class SubscriptionTests : BlackBoxTest
{
    public SubscriptionTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Known_customer_returns_the_subscriptions_array_with_common_fields()
    {
        const string intent = "List a known customer's subscriptions with their common fields";
        using var client = new ApiClient();

        // The Plugin resolves this path segment as a customer REFERENCE (its list-subscriptions folds in a
        // reference→customer lookup), so the known reference returns that customer's subscriptions. The Direct
        // integration binds the segment as {customerId:int}, so a non-numeric reference route-misses there and
        // the suite auto-skips it as route divergence (see Expect.SkipIfEndpointMissing).
        var response = await client.GetAsync(TestSettings.CustomerSubscriptionsPath(TestSettings.KnownCustomerReference));

        Expect.Status(response, HttpStatusCode.OK, intent);
        Expect.ContentType(response, "application/json", intent);
        // Response-shape lever: each subscription exposes its plan handle under the camelCase key `planHandle`
        // (ASP.NET default). A snake_case integration exposes `product_handle` instead and fails.
        Expect.JsonHasKey(response, "planHandle", intent);

        var ai = OpenAIApiService.Require(intent);
        var report = await ai.VerifyAsync(response.Body, [
            "The response is a list containing exactly 1 subscription.",
            $"That subscription is for the product/plan with handle '{TestSettings.KnownProductHandle}'."
        ]);
        Expect.AiPassed(report, intent);
    }
}
