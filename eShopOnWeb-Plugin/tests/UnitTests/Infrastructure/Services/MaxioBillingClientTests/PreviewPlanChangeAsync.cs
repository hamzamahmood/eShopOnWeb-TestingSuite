using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using MaxioAdvancedBilling;
using MaxioAdvancedBilling.Core.Authentication.Basic;
using MaxioAdvancedBilling.Core.Configuration;
using MaxioAdvancedBilling.Servers;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;
using Microsoft.eShopWeb.Infrastructure.Services;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure.Services.MaxioBillingClientTests;

/// <summary>
/// Regression coverage for a bug found via a live sandbox smoke test: the preview amount must be the net
/// amount actually due (payment_due_in_cents), not prorated_adjustment_in_cents alone - the latter is only
/// the old-plan credit component and is negative even on a genuine upgrade.
/// </summary>
public class PreviewPlanChangeAsync
{
    private static MaxioBillingClient BuildWithPathBasedResponses(string subscriptionJson, string migrationJson)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var body = request.RequestUri!.AbsolutePath.Contains("/migrations/preview")
                ? migrationJson
                : subscriptionJson;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        });

        var options = new MaxioAdvancedBillingClientOptions
        {
            Environment = ServerEnvironment.Us,
            BasicAuth = new BasicAuthCredentials { Username = "test-key", Password = "x" },
            Retry = RetryOptions.Default() with { StatusCodesToRetry = System.Array.Empty<HttpStatusCode>() }
        };
        options.Server.Production.Us.Site = "test-site";

        var sdkClient = new MaxioAdvancedBillingClient(new HttpClient(handler), options);
        return new MaxioBillingClient(sdkClient, Options.Create(TestClientFactory.Settings), Substitute.For<IAppLogger<MaxioBillingClient>>());
    }

    private const string SubscriptionJson = """
    {
      "subscription": {
        "id": 92971408,
        "state": "active",
        "product": { "handle": "basic-plan", "name": "Basic Plan" },
        "customer": { "reference": "buyer@test.com" }
      }
    }
    """;

    [Fact]
    public async Task UpgradeReportsThePositiveAmountActuallyDueNow()
    {
        var migrationJson = """{ "migration": { "prorated_adjustment_in_cents": -2900, "charge_in_cents": 29900, "payment_due_in_cents": 27000, "credit_applied_in_cents": -2900 } }""";
        var sut = BuildWithPathBasedResponses(SubscriptionJson, migrationJson);

        var quote = await sut.PreviewPlanChangeAsync("92971408", "eshop-pro", PlanChangeTiming.Immediate, default);

        Assert.Equal(270m, quote.ProratedAmount);
    }

    [Fact]
    public async Task DowngradeWithNothingDueReportsTheCreditInstead()
    {
        var migrationJson = """{ "migration": { "prorated_adjustment_in_cents": -29900, "charge_in_cents": 2904, "payment_due_in_cents": 0, "credit_applied_in_cents": -26996 } }""";
        var sut = BuildWithPathBasedResponses(SubscriptionJson, migrationJson);

        var quote = await sut.PreviewPlanChangeAsync("92971408", "basic-plan", PlanChangeTiming.Immediate, default);

        Assert.Equal(-269.96m, quote.ProratedAmount);
    }
}
