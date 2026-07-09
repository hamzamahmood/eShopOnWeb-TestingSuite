using System;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Configuration;
using Microsoft.eShopWeb.Infrastructure.Services;

// Standalone verification host that exposes the real MaxioBillingClient over HTTP so the black-box
// suite can exercise it. It wires ONLY the Maxio billing seam — no EF/LocalDB, no eShop catalog or
// identity database — and points every outbound Maxio call at the already-running mock server.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Reuse the repo's real Maxio wiring verbatim (see ConfigureCoreServices / PublicApi Program.cs):
// a typed HttpClient whose BaseAddress and HTTP Basic credentials come from MaxioSettings. Because
// the "Maxio:BaseUrl" override in appsettings.json wins over the subdomain-derived host
// (MaxioSettings.ResolveBaseUrl), the identical client is retargeted at the mock purely through
// configuration — no code change and no fork of the client.
builder.Services.Configure<MaxioSettings>(builder.Configuration.GetSection("Maxio"));
builder.Services.AddHttpClient<IBillingClient, MaxioBillingClient>((sp, http) =>
{
    var settings = sp.GetRequiredService<IOptions<MaxioSettings>>().Value;

    if (!string.IsNullOrWhiteSpace(settings.BaseUrl) || !string.IsNullOrWhiteSpace(settings.Subdomain))
    {
        http.BaseAddress = new Uri(settings.ResolveBaseUrl() + "/");
    }

    var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{settings.ApiKey}:x"));
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

var app = builder.Build();

app.MapControllers();

app.Run();
