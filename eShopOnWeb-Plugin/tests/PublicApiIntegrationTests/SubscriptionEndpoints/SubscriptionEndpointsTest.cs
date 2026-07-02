using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace PublicApiIntegrationTests.SubscriptionEndpoints;

/// <summary>
/// IBillingClient (not the Maxio SDK transport) is the seam here: this project verifies auth, routing, and
/// request/response shape for the subscription endpoints, not the SDK transport itself (that is covered by
/// the fake-HttpMessageHandler MaxioBillingClientTests in UnitTests) - per J5, no real outbound HTTP call is
/// made in any of these tests.
/// </summary>
[TestClass]
public class SubscriptionEndpointsTest
{
    private static (HttpClient Client, IBillingClient BillingClient) CreateClientWithFakeBillingClient()
    {
        var fakeBillingClient = Substitute.For<IBillingClient>();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.AddScoped(_ => fakeBillingClient);
            });
        });
        return (factory.CreateClient(), fakeBillingClient);
    }

    [TestMethod]
    public async Task ListPlans_ReturnsPlansWithoutAuthentication()
    {
        var (client, billingClient) = CreateClientWithFakeBillingClient();
        billingClient.ListPlansAsync(Arg.Any<CancellationToken>())
            .Returns(new List<PlanDto> { new("eshop-pro", "Pro Plan", 299m, 1, "month", false) });

        var response = await client.GetAsync("api/subscriptions/plans");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(body, "eshop-pro");
    }

    [TestMethod]
    public async Task Subscribe_RequiresAuthentication()
    {
        var (client, _) = CreateClientWithFakeBillingClient();
        var body = new StringContent("{\"ProductHandle\":\"eshop-pro\"}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("api/subscriptions", body);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task Subscribe_ValidTokenCreatesSubscription()
    {
        var (client, billingClient) = CreateClientWithFakeBillingClient();
        billingClient.FindOrCreateCustomerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("12345");
        billingClient.CreateSubscriptionAsync("12345", "eshop-pro", Arg.Any<CancellationToken>())
            .Returns(new SubscriptionDto("999", "demouser@microsoft.com", "eshop-pro", "Pro Plan", 299m, SubscriptionState.Active, 299m, null));

        var token = ApiTokenHelper.GetNormalUserToken();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var body = new StringContent("{\"ProductHandle\":\"eshop-pro\"}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("api/subscriptions", body);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    [TestMethod]
    public async Task RecordUsage_NonOwnerNonAdminGetsNotFoundRatherThanSomeoneElsesData()
    {
        var (client, billingClient) = CreateClientWithFakeBillingClient();
        billingClient.ReadSubscriptionAsync("999", Arg.Any<CancellationToken>())
            .Returns(new SubscriptionDto("999", "someone-else@microsoft.com", "eshop-pro", "Pro Plan", 299m, SubscriptionState.Active, 299m, null));

        var token = ApiTokenHelper.GetNormalUserToken(); // demouser@microsoft.com - not an admin, not the owner
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var body = new StringContent("{\"Quantity\":1,\"RequestId\":\"r1\"}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("api/subscriptions/999/usage", body);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        await billingClient.DidNotReceive().RecordUsageAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task RecordUsage_AdminCanRecordUsageOnAnyonesSubscription()
    {
        var (client, billingClient) = CreateClientWithFakeBillingClient();
        billingClient.ReadSubscriptionAsync("999", Arg.Any<CancellationToken>())
            .Returns(new SubscriptionDto("999", "someone-else@microsoft.com", "eshop-pro", "Pro Plan", 299m, SubscriptionState.Active, 299m, null));
        billingClient.RecordUsageAsync("999", 1m, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new UsageDto("u1", 1m, null, null));

        var token = ApiTokenHelper.GetAdminUserToken();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var body = new StringContent("{\"Quantity\":1,\"RequestId\":\"r1\"}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("api/subscriptions/999/usage", body);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
