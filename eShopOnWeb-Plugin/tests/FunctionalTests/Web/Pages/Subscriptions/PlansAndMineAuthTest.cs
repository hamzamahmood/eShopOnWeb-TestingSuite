using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Pages.Subscriptions;

[Collection("Sequential")]
public class PlansAndMineAuthTest : IClassFixture<TestApplication>
{
    public PlansAndMineAuthTest(TestApplication factory)
    {
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task PlansRedirectsAnonymousUserToLogin()
    {
        var response = await Client.GetAsync("/Subscriptions/Plans");
        var redirectLocation = response!.Headers.Location!.OriginalString;

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", redirectLocation);
    }

    [Fact]
    public async Task MineRedirectsAnonymousUserToLogin()
    {
        var response = await Client.GetAsync("/Subscriptions/Mine");
        var redirectLocation = response!.Headers.Location!.OriginalString;

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", redirectLocation);
    }
}
