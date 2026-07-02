using System;
using System.Net;
using System.Net.Http;
using System.Text;
using MaxioAdvancedBilling;
using MaxioAdvancedBilling.Core.Authentication.Basic;
using MaxioAdvancedBilling.Core.Configuration;
using MaxioAdvancedBilling.Servers;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Configuration;
using Microsoft.eShopWeb.Infrastructure.Services;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure.Services.MaxioBillingClientTests;

internal static class TestClientFactory
{
    public static readonly MaxioSettings Settings = new()
    {
        ApiKey = "test-key",
        Subdomain = "test-site",
        Environment = "US",
        ProductFamilyHandle = "eshop-subscribe",
        ProductFamilyId = 3008866,
        DefaultProductHandle = "eshop-pro",
        DefaultProductId = 7111477,
        AlternateProductHandle = "basic-plan",
        AlternateProductId = 7111478,
        MeteredComponentHandle = "api-call",
        MeteredComponentId = 3033795
    };

    public static MaxioBillingClient Build(HttpStatusCode status, string json)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var options = new MaxioAdvancedBillingClientOptions
        {
            Environment = ServerEnvironment.Us,
            BasicAuth = new BasicAuthCredentials { Username = "test-key", Password = "x" },
            // No status codes to retry -> the stubbed request is never re-issued, regardless of MaxRetries
            // (Polly requires MaxRetries >= 1, so an empty retry-status list is how a test disables retries).
            Retry = RetryOptions.Default() with { StatusCodesToRetry = Array.Empty<HttpStatusCode>() }
        };
        options.Server.Production.Us.Site = "test-site";

        var httpClient = new HttpClient(handler);
        var sdkClient = new MaxioAdvancedBillingClient(httpClient, options);

        return new MaxioBillingClient(sdkClient, Options.Create(Settings), Substitute.For<IAppLogger<MaxioBillingClient>>());
    }
}
