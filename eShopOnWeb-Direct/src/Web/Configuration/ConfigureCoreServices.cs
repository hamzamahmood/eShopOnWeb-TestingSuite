using System;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.Infrastructure.Configuration;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Data.Queries;
using Microsoft.eShopWeb.Infrastructure.Logging;
using Microsoft.eShopWeb.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Web.Configuration;

public static class ConfigureCoreServices
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped(typeof(IReadRepository<>), typeof(EfRepository<>));
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

        services.AddScoped<IBasketService, BasketService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IBasketQueryService, BasketQueryService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();

        var catalogSettings = configuration.Get<CatalogSettings>() ?? new CatalogSettings();
        services.AddSingleton<IUriComposer>(new UriComposer(catalogSettings));

        services.AddScoped(typeof(IAppLogger<>), typeof(LoggerAdapter<>));
        services.AddTransient<IEmailSender, EmailSender>();

        // Maxio subscription feature: typed HttpClient whose BaseAddress and HTTP Basic credentials come
        // from MaxioSettings, so the same build can target production, a dev/sandbox tenant, or a local
        // mock purely through configuration (plan §2.3 / §4.3). Explicit Maxio:BaseUrl wins over the
        // subdomain-derived host — do NOT hardcode the host.
        services.Configure<MaxioSettings>(configuration.GetSection("Maxio"));
        services.AddHttpClient<IBillingClient, MaxioBillingClient>((sp, http) =>
        {
            var settings = sp.GetRequiredService<IOptions<MaxioSettings>>().Value;

            // Only set the BaseAddress when Maxio is configured; if it isn't, the client fails at call time
            // with a clear error rather than coupling unrelated flows to the integration's configuration.
            if (!string.IsNullOrWhiteSpace(settings.BaseUrl) || !string.IsNullOrWhiteSpace(settings.Subdomain))
            {
                http.BaseAddress = new Uri(settings.ResolveBaseUrl() + "/");
            }

            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{settings.ApiKey}:x"));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        return services;
    }
}
