using System;
using MaxioAdvancedBilling;
using MaxioAdvancedBilling.Core.Authentication.Basic;
using MaxioAdvancedBilling.Core.Configuration;
using MaxioAdvancedBilling.Servers;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Configuration;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure;

public static class Dependencies
{
    public static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        bool useOnlyInMemoryDatabase = false;
        if (configuration["UseOnlyInMemoryDatabase"] != null)
        {
            useOnlyInMemoryDatabase = bool.Parse(configuration["UseOnlyInMemoryDatabase"]!);
        }

        if (useOnlyInMemoryDatabase)
        {
            services.AddDbContext<CatalogContext>(c =>
               c.UseInMemoryDatabase("Catalog"));

            services.AddDbContext<AppIdentityDbContext>(options =>
                options.UseInMemoryDatabase("Identity"));
        }
        else
        {
            // use real database
            // Requires LocalDB which can be installed with SQL Server Express 2016
            // https://www.microsoft.com/en-us/download/details.aspx?id=54284
            services.AddDbContext<CatalogContext>(c =>
                c.UseSqlServer(configuration.GetConnectionString("CatalogConnection")));

            // Add Identity DbContext
            services.AddDbContext<AppIdentityDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("IdentityConnection")));
        }
    }

    /// <summary>
    /// Wires the single point of contact with Maxio Advanced Billing: binds <see cref="MaxioSettings"/>,
    /// registers the generated SDK client (sandbox-only base URL, HTTPS enforced by the SDK itself, no
    /// auto-redirect, request logging, tuned per-attempt timeout), and registers <see cref="IBillingClient"/>.
    /// Called from both <c>Web</c> and <c>PublicApi</c>, alongside <see cref="ConfigureServices"/>.
    /// </summary>
    public static void ConfigureMaxioServices(IConfiguration configuration, IServiceCollection services)
    {
        services.Configure<MaxioSettings>(configuration.GetSection("Maxio"));
        var maxioSettings = configuration.GetSection("Maxio").Get<MaxioSettings>() ?? new MaxioSettings();

        services.AddTransient<MaxioRequestLoggingHandler>();
        services.AddHttpClient(Options.DefaultName)
            .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false })
            .AddHttpMessageHandler<MaxioRequestLoggingHandler>();

        services.AddMaxioAdvancedBillingClient(options =>
        {
            // options.Environment = string.Equals(maxioSettings.Environment, "EU", StringComparison.OrdinalIgnoreCase)
            //     ? ServerEnvironment.Eu
            //     : ServerEnvironment.Us;
            //
            // options.Server.Production.Us.Site = maxioSettings.Subdomain;
            // options.Server.Production.Eu.Site = maxioSettings.Subdomain;

            options.Server.Production.Us = new ProductionOptions.UsOptions
            {
                BaseUrl = "http://localhost:8080"
            };

            options.BasicAuth = new BasicAuthCredentials
            {
                Username = maxioSettings.ApiKey,
                Password = "x"
            };
            options.Retry = RetryOptions.Default() with
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
        });

        services.AddScoped<IBillingClient, MaxioBillingClient>();
        // Raw passthrough for the test-harness controller: uses the same SDK client, but returns Maxio's
        // response verbatim (serialized SDK model on success; exact status + body on error).
        services.AddScoped<IMaxioPassthrough, MaxioPassthroughClient>();
        services.AddSingleton<IIdempotencyCache, MemoryIdempotencyCache>();
    }
}
