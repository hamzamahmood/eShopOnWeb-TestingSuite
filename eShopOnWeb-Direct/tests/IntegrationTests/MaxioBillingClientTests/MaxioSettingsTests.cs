using System;
using Microsoft.eShopWeb.Infrastructure.Configuration;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.MaxioBillingClientTests;

public class MaxioSettingsTests
{
    [Fact]
    public void ExplicitBaseUrlWinsOverSubdomain()
    {
        var settings = new MaxioSettings { Subdomain = "apimatic-hackathon", BaseUrl = "http://localhost:8080/" };
        Assert.Equal("http://localhost:8080", settings.ResolveBaseUrl());
    }

    [Fact]
    public void DerivesUsHostFromSubdomainWhenNoOverride()
    {
        var settings = new MaxioSettings { Subdomain = "apimatic-hackathon", Environment = "US" };
        Assert.Equal("https://apimatic-hackathon.chargify.com", settings.ResolveBaseUrl());
    }

    [Fact]
    public void DerivesEuHostFromSubdomainForEuRegion()
    {
        var settings = new MaxioSettings { Subdomain = "apimatic-hackathon", Environment = "EU" };
        Assert.Equal("https://apimatic-hackathon.ebilling.maxio.com", settings.ResolveBaseUrl());
    }

    [Fact]
    public void ThrowsWhenNeitherBaseUrlNorSubdomainConfigured()
    {
        var settings = new MaxioSettings();
        Assert.Throws<InvalidOperationException>(() => settings.ResolveBaseUrl());
    }
}
