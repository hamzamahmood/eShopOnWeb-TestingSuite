using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure.Services.MaxioBillingClientTests;

public class FindCustomerIdAsync
{
    [Fact]
    public async Task ReturnsNullOn404WithoutThrowing()
    {
        var sut = TestClientFactory.Build(HttpStatusCode.NotFound, """{ "errors": ["not found"] }""");

        var result = await sut.FindCustomerIdAsync("nobody@test.com", default);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReturnsTheCustomerIdWhenFound()
    {
        var json = """{ "customer": { "id": 555, "reference": "buyer@test.com" } }""";
        var sut = TestClientFactory.Build(HttpStatusCode.OK, json);

        var result = await sut.FindCustomerIdAsync("buyer@test.com", default);

        Assert.Equal("555", result);
    }
}
