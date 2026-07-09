using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.Infrastructure.Configuration;
using Microsoft.eShopWeb.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.MaxioBillingClientTests;

public class MaxioBillingClientTests
{
    private const int FamilyId = 3008866;
    private const int MeteredId = 3033795;

    private static MaxioSettings Settings() => new()
    {
        ApiKey = "test-key",
        Subdomain = "apimatic-hackathon",
        ProductFamilyId = FamilyId,
        MeteredComponentHandle = "api-call",
        MeteredComponentId = MeteredId
    };

    private static MaxioBillingClient CreateClient(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://apimatic-hackathon.chargify.com/") };
        return new MaxioBillingClient(http, Options.Create(Settings()));
    }

    [Fact]
    public async Task ListPlansMapsPriceIntervalAndFiltersArchived()
    {
        var json = """
        [
          { "product": { "id": 7111477, "name": "Pro Plan", "handle": "eshop-pro", "price_in_cents": 29900, "interval": 1, "interval_unit": "month", "product_family": { "handle": "eshop-subscribe" } } },
          { "product": { "id": 999, "name": "Old", "handle": "old", "price_in_cents": 100, "interval": 1, "interval_unit": "month", "archived_at": "2020-01-01T00:00:00-05:00" } }
        ]
        """;
        var handler = new StubHttpMessageHandler().Map(HttpMethod.Get, $"product_families/{FamilyId}/products.json", json);
        var client = CreateClient(handler);

        var plans = (await client.ListPlansAsync()).ToList();

        Assert.Single(plans);
        Assert.Equal("eshop-pro", plans[0].Handle);
        Assert.Equal(29900, plans[0].PriceInCents);
        Assert.Equal(299.00m, plans[0].Price);
        Assert.Equal("month", plans[0].IntervalUnit);
    }

    [Fact]
    public async Task EnsureCustomerCreatesWhenLookupReturns404()
    {
        var reference = "demo@x.com";
        var handler = new StubHttpMessageHandler()
            .Map(HttpMethod.Get, $"customers/lookup.json?reference={Uri.EscapeDataString(reference)}", "\"not found\"", HttpStatusCode.NotFound)
            .Map(HttpMethod.Post, "customers.json", """{ "customer": { "id": 555, "email": "demo@x.com", "reference": "demo@x.com" } }""");
        var client = CreateClient(handler);

        var id = await client.EnsureCustomerAsync(reference, reference, null, null);

        Assert.Equal(555, id);
        var post = handler.Requests.Single(r => r.Method == HttpMethod.Post && r.PathAndQuery == "customers.json");
        Assert.Contains("\"reference\":\"demo@x.com\"", post.Body);
        Assert.Contains("\"email\":\"demo@x.com\"", post.Body);
    }

    [Fact]
    public async Task EnsureCustomerReturnsExistingWithoutCreating()
    {
        var reference = "demo@x.com";
        var handler = new StubHttpMessageHandler()
            .Map(HttpMethod.Get, $"customers/lookup.json?reference={Uri.EscapeDataString(reference)}",
                """{ "customer": { "id": 42, "reference": "demo@x.com" } }""");
        var client = CreateClient(handler);

        var id = await client.EnsureCustomerAsync(reference, reference, null, null);

        Assert.Equal(42, id);
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task CreateSubscriptionPostsCustomerAndHandleAndMapsState()
    {
        var handler = new StubHttpMessageHandler().Map(HttpMethod.Post, "subscriptions.json",
            """{ "subscription": { "id": 700, "state": "active", "product_price_in_cents": 29900, "product": { "handle": "eshop-pro", "name": "Pro Plan", "interval": 1, "interval_unit": "month" }, "customer": { "id": 42, "reference": "demo@x.com" } } }""");
        var client = CreateClient(handler);

        var sub = await client.CreateSubscriptionAsync(42, "eshop-pro");

        Assert.Equal(700, sub.Id);
        Assert.Equal(SubscriptionState.Active, sub.State);
        Assert.Equal("eshop-pro", sub.ProductHandle);
        Assert.Equal(29900, sub.ProductPriceInCents);
        var post = handler.Requests.Single();
        Assert.Contains("\"customer_id\":42", post.Body);
        Assert.Contains("\"product_handle\":\"eshop-pro\"", post.Body);
    }

    [Fact]
    public async Task GetMeteredComponentMapsKind()
    {
        var handler = new StubHttpMessageHandler().Map(HttpMethod.Get, "components/lookup.json?handle=api-call",
            """{ "component": { "id": 3033795, "handle": "api-call", "name": "API Calls", "kind": "metered_component" } }""");
        var client = CreateClient(handler);

        var component = await client.GetMeteredComponentAsync();

        Assert.True(component.IsMetered);
        Assert.Equal(BillingComponentKind.Metered, component.Kind);
    }

    [Fact]
    public async Task RecordUsagePostsQuantityToUsagesPath()
    {
        var handler = new StubHttpMessageHandler().Map(HttpMethod.Post,
            $"subscriptions/700/components/{MeteredId}/usages.json", """{ "usage": { "id": 1, "quantity": 5 } }""");
        var client = CreateClient(handler);

        await client.RecordUsageAsync(700, 5, "memo");

        var post = handler.Requests.Single();
        Assert.Contains("\"quantity\":5", post.Body);
        Assert.Contains("\"memo\":\"memo\"", post.Body);
    }

    [Fact]
    public async Task GetUsageTotalReadsUnitBalance()
    {
        var handler = new StubHttpMessageHandler().Map(HttpMethod.Get,
            $"subscriptions/700/components/{MeteredId}.json", """{ "component": { "component_id": 3033795, "kind": "metered_component", "unit_balance": 17 } }""");
        var client = CreateClient(handler);

        var total = await client.GetUsageTotalAsync(700);

        Assert.Equal(17, total);
    }

    [Fact]
    public async Task PreviewImmediatePostsMigrationPreviewAndMapsCents()
    {
        var handler = new StubHttpMessageHandler().Map(HttpMethod.Post, "subscriptions/700/migrations/preview.json",
            """{ "migration": { "prorated_adjustment_in_cents": 1000, "charge_in_cents": 1500, "payment_due_in_cents": 500, "credit_applied_in_cents": 1000 } }""");
        var client = CreateClient(handler);

        var preview = await client.PreviewPlanChangeAsync(700, "basic-plan", PlanChangeTiming.Immediate);

        Assert.Equal(1500, preview.ChargeInCents);
        Assert.Equal(500, preview.PaymentDueInCents);
        Assert.Contains("\"product_handle\":\"basic-plan\"", handler.Requests.Single().Body);
    }

    [Fact]
    public async Task PreviewAtRenewalReturnsZeroWithoutCallingProvider()
    {
        var handler = new StubHttpMessageHandler();
        var client = CreateClient(handler);

        var preview = await client.PreviewPlanChangeAsync(700, "basic-plan", PlanChangeTiming.AtRenewal);

        Assert.Equal(0, preview.ChargeInCents);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ChangePlanImmediatePostsMigrations()
    {
        var handler = new StubHttpMessageHandler().Map(HttpMethod.Post, "subscriptions/700/migrations.json",
            """{ "subscription": { "id": 700, "state": "active", "product": { "handle": "basic-plan" } } }""");
        var client = CreateClient(handler);

        var updated = await client.ChangePlanAsync(700, "basic-plan", PlanChangeTiming.Immediate);

        Assert.Equal("basic-plan", updated.ProductHandle);
        Assert.Contains("\"preserve_period\":true", handler.Requests.Single().Body);
    }

    [Fact]
    public async Task ChangePlanAtRenewalPutsSubscriptionWithDelayedFlag()
    {
        var handler = new StubHttpMessageHandler().Map(HttpMethod.Put, "subscriptions/700.json",
            """{ "subscription": { "id": 700, "state": "active", "product": { "handle": "eshop-pro" } } }""");
        var client = CreateClient(handler);

        await client.ChangePlanAsync(700, "basic-plan", PlanChangeTiming.AtRenewal);

        var put = handler.Requests.Single();
        Assert.Equal(HttpMethod.Put, put.Method);
        Assert.Contains("\"product_change_delayed\":true", put.Body);
    }

    [Fact]
    public async Task CancelUsesHttpDelete()
    {
        var handler = new StubHttpMessageHandler().Map(HttpMethod.Delete, "subscriptions/700.json",
            """{ "subscription": { "id": 700, "state": "canceled" } }""");
        var client = CreateClient(handler);

        var updated = await client.ApplyLifecycleActionAsync(700, SubscriptionLifecycleAction.Cancel, "bye");

        Assert.Equal(SubscriptionState.Canceled, updated.State);
        Assert.Equal(HttpMethod.Delete, handler.Requests.Single().Method);
    }

    [Fact]
    public async Task CancelAtEndOfPeriodPostsDelayedCancelThenRereadsSubscription()
    {
        var handler = new StubHttpMessageHandler()
            .Map(HttpMethod.Post, "subscriptions/700/delayed_cancel.json", """{ "message": "will cancel" }""")
            .Map(HttpMethod.Get, "subscriptions/700.json",
                """{ "subscription": { "id": 700, "state": "active", "cancel_at_end_of_period": true } }""");
        var client = CreateClient(handler);

        var updated = await client.ApplyLifecycleActionAsync(700, SubscriptionLifecycleAction.CancelAtEndOfPeriod, null);

        Assert.True(updated.CancelAtEndOfPeriod);
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Post && r.PathAndQuery == "subscriptions/700/delayed_cancel.json");
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Get && r.PathAndQuery == "subscriptions/700.json");
    }

    [Fact]
    public async Task NonSuccessResponseThrowsBillingProviderExceptionWithMessage()
    {
        var handler = new StubHttpMessageHandler().Map(HttpMethod.Post, "subscriptions.json",
            """{ "errors": ["Product handle: could not be found."] }""", HttpStatusCode.UnprocessableEntity);
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<BillingProviderException>(() => client.CreateSubscriptionAsync(42, "nope"));
        Assert.Contains("could not be found", ex.Message);
        Assert.Equal(422, ex.StatusCode);
    }

    [Fact]
    public async Task MissingBaseAddressThrowsClearBillingProviderException()
    {
        var http = new HttpClient(new StubHttpMessageHandler()); // no BaseAddress => Maxio not configured
        var client = new MaxioBillingClient(http, Options.Create(Settings()));

        var ex = await Assert.ThrowsAsync<BillingProviderException>(() => client.ListPlansAsync());
        Assert.Contains("not configured", ex.Message);
    }
}
