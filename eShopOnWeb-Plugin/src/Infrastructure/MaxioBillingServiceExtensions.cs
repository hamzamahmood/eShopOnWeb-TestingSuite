using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.Infrastructure.Configuration;
using Microsoft.eShopWeb.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.eShopWeb.Infrastructure;

// Single wiring point for the Maxio subscription integration, shared by both
// hosts (Web storefront and PublicApi). Registers the typed options, the typed
// HttpClient-backed billing client (the one place the provider is touched), and
// the domain subscription service — mirroring how the existing services are
// registered in the composition roots (plan §2.1 / §4.3).
public static class MaxioBillingServiceExtensions
{
    public static IServiceCollection AddMaxioBillingServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MaxioSettings>(configuration.GetSection("Maxio"));

        // Typed client via IHttpClientFactory. MaxioBillingClient resolves its own
        // BaseAddress from MaxioSettings (explicit Maxio:BaseUrl wins, else derived
        // from Subdomain + region) — the target server is configuration-driven and
        // never hardcoded (plan §2.3 / §4.3).
        services.AddHttpClient<IBillingClient, MaxioBillingClient>();

        services.AddScoped<ISubscriptionService, SubscriptionService>();

        return services;
    }
}
