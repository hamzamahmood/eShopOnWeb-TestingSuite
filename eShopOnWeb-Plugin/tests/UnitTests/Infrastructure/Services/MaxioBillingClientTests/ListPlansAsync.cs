using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure.Services.MaxioBillingClientTests;

public class ListPlansAsync
{
    [Fact]
    public async Task MapsProductFieldsToProviderAgnosticPlanDto()
    {
        var json = """
        [
          {
            "product": {
              "id": 7111477,
              "name": "Pro Plan",
              "handle": "eshop-pro",
              "price_in_cents": 29900,
              "interval": 1,
              "interval_unit": "month",
              "require_credit_card": false
            }
          }
        ]
        """;
        var sut = TestClientFactory.Build(HttpStatusCode.OK, json);

        var plans = await sut.ListPlansAsync(default);

        var plan = Assert.Single(plans);
        Assert.Equal("eshop-pro", plan.Handle);
        Assert.Equal("Pro Plan", plan.Name);
        Assert.Equal(299m, plan.Price);
        Assert.Equal(1, plan.IntervalCount);
        Assert.Equal("month", plan.IntervalUnit);
        Assert.False(plan.RequiresPaymentMethod);
    }
}
