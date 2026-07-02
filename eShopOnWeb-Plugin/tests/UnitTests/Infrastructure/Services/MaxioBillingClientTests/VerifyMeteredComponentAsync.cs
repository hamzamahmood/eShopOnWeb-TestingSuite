using System.Net;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure.Services.MaxioBillingClientTests;

public class VerifyMeteredComponentAsync
{
    [Fact]
    public async Task ThrowsWhenComponentIsNotMetered()
    {
        var json = """
        {
          "component": {
            "id": 3033795,
            "handle": "api-call",
            "kind": "quantity_based_component",
            "product_family_id": 3008866
          }
        }
        """;
        var sut = TestClientFactory.Build(HttpStatusCode.OK, json);

        await Assert.ThrowsAsync<MeteredComponentMisconfiguredException>(() => sut.VerifyMeteredComponentAsync(default));
    }

    [Fact]
    public async Task ThrowsWhenComponentBelongsToADifferentFamily()
    {
        var json = """
        {
          "component": {
            "id": 3033795,
            "handle": "api-call",
            "kind": "metered_component",
            "product_family_id": 999999
          }
        }
        """;
        var sut = TestClientFactory.Build(HttpStatusCode.OK, json);

        await Assert.ThrowsAsync<MeteredComponentMisconfiguredException>(() => sut.VerifyMeteredComponentAsync(default));
    }

    [Fact]
    public async Task ThrowsWhenComponentDoesNotExist()
    {
        var sut = TestClientFactory.Build(HttpStatusCode.NotFound, """{ "errors": ["not found"] }""");

        await Assert.ThrowsAsync<MeteredComponentMisconfiguredException>(() => sut.VerifyMeteredComponentAsync(default));
    }

    [Fact]
    public async Task PassesWhenComponentIsMeteredAndOnTheConfiguredFamily()
    {
        var json = """
        {
          "component": {
            "id": 3033795,
            "handle": "api-call",
            "kind": "metered_component",
            "product_family_id": 3008866
          }
        }
        """;
        var sut = TestClientFactory.Build(HttpStatusCode.OK, json);

        await sut.VerifyMeteredComponentAsync(default);
    }
}
