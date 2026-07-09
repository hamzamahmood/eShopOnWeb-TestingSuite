using Reference;

var builder = WebApplication.CreateBuilder(args);

var settings = new MaxioSettings();
builder.Configuration.GetSection("Maxio").Bind(settings);         // binds Maxio__* env vars
var breaks = Breaks.From(Environment.GetEnvironmentVariable("BREAK"));

// S3: fail fast at startup on missing required config (unless the app never boots -> gate catches it)
if (string.IsNullOrWhiteSpace(settings.ApiKey))
    throw new InvalidOperationException("Configuration error: Maxio:ApiKey is required and was not provided.");

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(breaks);
builder.Services.AddHttpClient<MaxioClient>();

var app = builder.Build();

// S1: the secret is NEVER logged (only the deliberate break does).
if (breaks.LogSecret)
    app.Logger.LogInformation("Maxio API key in use: {ApiKey}", settings.ApiKey);

// error-mapping middleware — clean bodies, no internals leaked (E1-E4) unless the leak break is on.
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (ValidationFailedException ex) { await Problem(ctx, 400, ex.Message, breaks, ex); }
    catch (NotFoundException ex)         { await Problem(ctx, 404, ex.Message, breaks, ex); }
    catch (ProviderRejectedException ex) { await Problem(ctx, 422, ex.Message, breaks, ex); }
    catch (ProviderUnavailableException ex) { await Problem(ctx, 502, ex.Message, breaks, ex); }
    catch (Exception ex)                 { await Problem(ctx, 500, "An unexpected error occurred.", breaks, ex); }
});

// ---- the 11 /api/billing endpoints ----
app.MapGet("/api/billing/plans", async (MaxioClient c, CancellationToken ct)
    => Results.Ok(await c.ListPlansAsync(ct)));

app.MapPost("/api/billing/customers", async (CreateCustomerReq req, MaxioClient c, CancellationToken ct) =>
{
    Require(("reference", req.Reference), ("firstName", req.FirstName), ("lastName", req.LastName), ("email", req.Email));
    var cust = await c.FindOrCreateCustomerAsync(req.Reference!, req.FirstName!, req.LastName!, req.Email!, ct);
    return Results.Ok(new { customerId = cust.Id, reference = cust.Reference });
});

app.MapGet("/api/billing/customers/{customerId:long}/subscriptions", async (long customerId, MaxioClient c, CancellationToken ct)
    => Results.Ok(await c.ListSubscriptionsAsync(customerId, ct)));

app.MapGet("/api/billing/subscriptions/{id:long}", async (long id, MaxioClient c, CancellationToken ct)
    => Results.Ok(await c.ReadSubscriptionAsync(id, ct)));

app.MapPost("/api/billing/subscriptions/{id:long}/pause", async (long id, MaxioClient c, CancellationToken ct)
    => Results.Ok(await c.PauseAsync(id, ct)));
app.MapPost("/api/billing/subscriptions/{id:long}/resume", async (long id, MaxioClient c, CancellationToken ct)
    => Results.Ok(await c.ResumeAsync(id, ct)));
app.MapPost("/api/billing/subscriptions/{id:long}/reactivate", async (long id, MaxioClient c, CancellationToken ct)
    => Results.Ok(await c.ReactivateAsync(id, ct)));

app.MapPost("/api/billing/subscriptions", async (CreateSubscriptionReq req, MaxioClient c, CancellationToken ct) =>
{
    Require(("customerReference", req.CustomerReference), ("productHandle", req.ProductHandle));
    return Results.Ok(await c.CreateSubscriptionAsync(req.CustomerReference!, req.ProductHandle!, ct));
});

app.MapPost("/api/billing/subscriptions/{id:long}/plan-change", async (long id, PlanChangeReq req, MaxioClient c, CancellationToken ct) =>
{
    Require(("productHandle", req.ProductHandle));
    return Results.Ok(await c.PlanChangeAsync(id, req.ProductHandle!, ct));
});

app.MapDelete("/api/billing/subscriptions/{id:long}", async (long id, MaxioClient c, CancellationToken ct)
    => Results.Ok(await c.CancelAsync(id, ct)));

app.MapPost("/api/billing/subscriptions/{id:long}/usage", async (long id, UsageReq req, MaxioClient c, CancellationToken ct) =>
{
    if (req.Quantity is null) throw new ValidationFailedException(new[] { "quantity is required." });
    return Results.Ok(await c.RecordUsageAsync(id, req.Quantity.Value, req.Memo, ct));
});

app.Run();

// ---- helpers / DTOs ----
static void Require(params (string Name, string? Value)[] fields)
{
    var missing = fields.Where(f => string.IsNullOrWhiteSpace(f.Value)).Select(f => $"{f.Name} is required.").ToList();
    if (missing.Count > 0) throw new ValidationFailedException(missing);
}

static async Task Problem(HttpContext ctx, int status, string message, Breaks breaks, Exception ex)
{
    if (ctx.Response.HasStarted) return;
    ctx.Response.StatusCode = status;
    ctx.Response.ContentType = "application/json";
    object payload = breaks.Leak
        ? new { error = message, detail = ex.ToString() }   // BREAK: leaks the exception/stack (E3)
        : new { error = message };
    await ctx.Response.WriteAsJsonAsync(payload);
}

public sealed record CreateCustomerReq(string? Reference, string? FirstName, string? LastName, string? Email);
public sealed record CreateSubscriptionReq(string? CustomerReference, string? ProductHandle);
public sealed record PlanChangeReq(string? ProductHandle);
public sealed record UsageReq(double? Quantity, string? Memo);
