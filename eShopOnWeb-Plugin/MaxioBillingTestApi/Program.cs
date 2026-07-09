using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure;
using Microsoft.eShopWeb.Infrastructure.Logging;

// Standalone Web API host that exposes the existing MaxioBillingClient (via the
// IBillingClient seam) over HTTP as a Maxio billing microservice, so the black-box
// verification suite can exercise it. It reuses the real Infrastructure client and
// wiring unchanged; only the Maxio config (appsettings.json) points the outbound
// base URL at the mock server. No EF/identity/catalog database is wired — this
// microservice needs only the Maxio services.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// IAppLogger<T> adapter the MaxioBillingClient depends on (same registration the
// eShop hosts use).
builder.Services.AddScoped(typeof(IAppLogger<>), typeof(LoggerAdapter<>));

// The repo's own single Maxio wiring point: typed options + the typed HttpClient
// backed IBillingClient. The client resolves its outbound base URL from
// MaxioSettings (explicit Maxio:BaseUrl wins), so pointing at the mock is a pure
// config change.
builder.Services.AddMaxioBillingServices(builder.Configuration);

var app = builder.Build();

app.MapControllers();

app.Run();
