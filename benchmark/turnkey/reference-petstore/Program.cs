using ReferencePetstore;

var builder = WebApplication.CreateBuilder(args);

var settings = new PetstoreSettings();
builder.Configuration.GetSection("Petstore").Bind(settings);         // binds Petstore__* env vars
var breaks = Breaks.From(Environment.GetEnvironmentVariable("BREAK"));

// S3: fail fast at startup on missing required config.
if (string.IsNullOrWhiteSpace(settings.ApiKey))
    throw new InvalidOperationException("Configuration error: Petstore:ApiKey is required and was not provided.");

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(breaks);
builder.Services.AddHttpClient<PetstoreClient>();

var app = builder.Build();

// S1: the secret is NEVER logged (only the deliberate break does).
if (breaks.LogSecret)
    app.Logger.LogInformation("Petstore api_key in use: {ApiKey} (org {OrgId})", settings.ApiKey, settings.OrgId);

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

// ---- the /api/store endpoints ----
app.MapGet("/api/store/pets", async (string? status, PetstoreClient c, Breaks bk, CancellationToken ct) =>
{
    if (bk.Hardcode)   // BREAK: canned pets WITHOUT calling upstream -> must fail the C1 upstream-call check
        return Results.Ok(new[] { new { id = 10, name = "doggie", status = "available" } });
    var pets = await c.FindByStatusAsync(status ?? "available", ct);
    if (bk.ShallowMap)   // BREAK (quality D1 low-anchor): drop list cardinality -> only the first pet
        return Results.Ok(pets.Take(1));
    return Results.Ok(pets);
});

app.MapGet("/api/store/pets/by-tags", async (string? tags, PetstoreClient c, CancellationToken ct)
    => Results.Ok(await c.FindByTagsAsync(tags ?? "friendly", ct)));

app.MapGet("/api/store/pets/{petId:long}", async (long petId, PetstoreClient c, CancellationToken ct)
    => Results.Ok(await c.GetPetAsync(petId, ct)));

app.MapPost("/api/store/pets", async (CreatePetReq req, PetstoreClient c, CancellationToken ct) =>
{
    Require(("name", req.Name), ("photoUrls", req.PhotoUrls is { Count: > 0 } ? "ok" : null));
    return Results.Ok(await c.CreatePetAsync(req.Name!, req.PhotoUrls!, req.Status, ct));
});

app.MapGet("/api/store/orders/{orderId:long}", async (long orderId, PetstoreClient c, CancellationToken ct)
    => Results.Ok(await c.GetOrderAsync(orderId, ct)));

app.MapPost("/api/store/orders", async (CreateOrderReq req, PetstoreClient c, CancellationToken ct) =>
{
    if (req.PetId is null || req.Quantity is null)
        throw new ValidationFailedException(new[] { "petId and quantity are required." });
    return Results.Ok(await c.CreateOrderAsync(req.PetId.Value, req.Quantity.Value, req.Status, ct));
});

app.MapGet("/api/store/users/{username}", async (string username, PetstoreClient c, CancellationToken ct)
    => Results.Ok(await c.GetUserAsync(username, ct)));

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

public sealed record CreatePetReq(string? Name, List<string>? PhotoUrls, string? Status);
public sealed record CreateOrderReq(long? PetId, long? Quantity, string? Status);
