using System.Net;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure.Services.MaxioBillingClientTests;

public class RecordUsageAsync
{
    [Fact]
    public async Task MapsAServerErrorToBillingProviderExceptionRatherThanLeakingTheSdkException()
    {
        var sut = TestClientFactory.Build(HttpStatusCode.InternalServerError, """{ "errors": ["boom"] }""");

        await Assert.ThrowsAsync<BillingProviderException>(() => sut.RecordUsageAsync("999", 1m, "memo", default));
    }

    [Fact]
    public async Task ReturnsTheCreatedUsageOnSuccess()
    {
        var json = """
        {
          "usage": {
            "id": 42,
            "quantity": 1,
            "memo": "memo",
            "created_at": "2026-01-01T00:00:00Z"
          }
        }
        """;
        var sut = TestClientFactory.Build(HttpStatusCode.OK, json);

        var usage = await sut.RecordUsageAsync("999", 1m, "memo", default);

        Assert.Equal("42", usage.UsageId);
        Assert.Equal(1m, usage.Quantity);
        Assert.Equal("memo", usage.Memo);
    }
}
