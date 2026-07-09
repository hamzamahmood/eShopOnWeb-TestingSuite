using System.Text;
using System.Text.Json;
using MaxioMock;

var builder = WebApplication.CreateBuilder(args);
var listenUrl = Environment.GetEnvironmentVariable("MOCK_URL") ?? "http://localhost:8080";
builder.WebHost.UseUrls(listenUrl);
var app = builder.Build();

var recorder = new Recorder();
var state = new MockState();
var faults = new FaultEngine();
var json = new JsonSerializerOptions { PropertyNamingPolicy = null };   // emit exact snake_case identifiers
var webJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);

// ---- helpers ------------------------------------------------------------------------------------
IResult J(object? obj, int status = 200) => Results.Json(obj, json, statusCode: status);
IResult Errors422(params string[] msgs) => J(new { errors = msgs }, 422);

bool TryProp(JsonElement e, string name, out JsonElement v)
{
    v = default;
    return e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out v);
}
string? Str(JsonElement e, string name)
    => TryProp(e, name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

async Task<JsonElement> ReadBody(HttpContext ctx)
{
    ctx.Request.Body.Position = 0;
    using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
    var text = await reader.ReadToEndAsync();
    ctx.Request.Body.Position = 0;
    if (string.IsNullOrWhiteSpace(text)) return default;
    try { using var doc = JsonDocument.Parse(text); return doc.RootElement.Clone(); }
    catch { return default; }
}

// ---- recording + optional auth-check middleware (Maxio routes only) -----------------------------
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    if (path.StartsWith("/__mock", StringComparison.OrdinalIgnoreCase)) { await next(); return; }

    ctx.Request.EnableBuffering();
    string? body = null;
    if (ctx.Request.ContentLength is > 0)
    {
        using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
        body = await reader.ReadToEndAsync();
        ctx.Request.Body.Position = 0;
    }
    var authHeader = ctx.Request.Headers.Authorization.ToString();
    var hasAuth = !string.IsNullOrWhiteSpace(authHeader);
    recorder.Record(ctx.Request.Method, path, ctx.Request.QueryString.Value ?? "", hasAuth, body);

    // fault injection (gate-controlled). Runs AFTER recording so R5/R6 counts still see the request.
    if (faults.Match(ctx.Request.Method, path) is { } f)
    {
        switch (f.Action)
        {
            case "status503":
                await Results.Json(new { errors = new[] { "upstream temporarily unavailable" } }, webJson, statusCode: 503).ExecuteAsync(ctx);
                return;
            case "status429":
                ctx.Response.Headers.RetryAfter = (f.RetryAfter > 0 ? f.RetryAfter : 1).ToString();
                await Results.Json(new { errors = new[] { "rate limited" } }, webJson, statusCode: 429).ExecuteAsync(ctx);
                return;
            case "malformed":
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync("{ \"subscription\": { not valid json ][");
                return;
            case "hang":
                try { await Task.Delay(TimeSpan.FromSeconds(f.RetryAfter > 0 ? f.RetryAfter : 65), ctx.RequestAborted); } catch { }
                return;
            case "reset":
                ctx.Abort();
                return;
        }
    }

    if (state.RequireAuth && !hasAuth)
    {
        await Results.Json(new { error = "authentication required" }, webJson, statusCode: 401)
                     .ExecuteAsync(ctx);
        return;
    }
    await next();
});

// ---- gate-only control endpoints (never part of the Maxio contract; arm never calls these) ------
app.MapGet("/__mock/health", () => Results.Ok(new { ok = true }));
app.MapGet("/__mock/recordings", () => J(new { records = recorder.Snapshot() }));
app.MapPost("/__mock/reset", () => { recorder.Reset(); state.Reset(); faults.Reset(); return Results.Ok(new { reset = true }); });
app.MapPost("/__mock/config", async (HttpContext ctx) =>
{
    var cfg = await JsonSerializer.DeserializeAsync<MockConfig>(ctx.Request.Body, webJson);
    if (cfg?.RequireAuth is bool ra) state.RequireAuth = ra;
    if (cfg?.Faults is not null)
        faults.SetRules(cfg.Faults.Select(d => new FaultRule(
            d.PathContains, d.Method, d.Action ?? "status503", d.Times ?? 1, d.RetryAfter ?? 0)));
    return Results.Ok(new { require_auth = state.RequireAuth, faults = cfg?.Faults?.Count ?? 0 });
});

// ---- the 11 Maxio wire routes -------------------------------------------------------------------

// 1. List plans
app.MapGet("/product_families/{pfId}/products.json", (string pfId) =>
    pfId != MockStore.ProductFamilyId.ToString()
        ? J("A valid product_family_id is required", 404)   // spec: bare JSON string body
        : J(MockStore.ProductsList()));

// 2a. Create customer  (note: 200, not 201)
app.MapPost("/customers.json", async (HttpContext ctx) =>
{
    var root = await ReadBody(ctx);
    if (!TryProp(root, "customer", out var c) || c.ValueKind != JsonValueKind.Object)
        return J(new { errors = new { customer = "can't be blank" } }, 422);
    var first = Str(c, "first_name");
    var last = Str(c, "last_name");
    var email = Str(c, "email");
    var missing = new List<string>();
    if (string.IsNullOrWhiteSpace(first)) missing.Add("First name: cannot be blank.");
    if (string.IsNullOrWhiteSpace(last)) missing.Add("Last name: cannot be blank.");
    if (string.IsNullOrWhiteSpace(email)) missing.Add("Email: cannot be blank.");
    if (missing.Count > 0) return J(new { errors = missing.ToArray() }, 422);
    return J(new { customer = MockStore.Customer(MockStore.CreatedCustomerId, first!, last!, email!, Str(c, "reference")) });
});

// 2b. Lookup customer by reference
app.MapGet("/customers/lookup.json", (string? reference) =>
{
    if (string.IsNullOrEmpty(reference)) return Results.StatusCode(404);
    return reference == MockStore.KnownCustomerReference
        ? J(new { customer = MockStore.KnownCustomer() })
        : Results.StatusCode(404);
});

// 3. List customer subscriptions
app.MapGet("/customers/{customerId}/subscriptions.json", (string customerId) =>
    customerId != MockStore.KnownCustomerId.ToString()
        ? Results.StatusCode(404)
        : J(new[] { new { subscription = MockStore.ActiveSubscription() } }));

// 4. Read subscription
app.MapGet("/subscriptions/{sid}.json", (string sid) =>
{
    var found = MockStore.SubscriptionById(sid);
    return found is null ? Results.StatusCode(404) : J(new { subscription = found.Value.sub });
});

// 5. Pause (hold)
app.MapPost("/subscriptions/{sid}/hold.json", (string sid) =>
{
    var found = MockStore.SubscriptionById(sid);
    if (found is null) return Results.StatusCode(404);
    return found.Value.state == "active"
        ? J(new { subscription = MockStore.Subscription(int.Parse(sid), "on_hold", MockStore.ProProduct(), MockStore.KnownCustomer()) })
        : Errors422("This subscription is not eligible to be put on hold.");
});

// 6. Resume
app.MapPost("/subscriptions/{sid}/resume.json", (string sid) =>
{
    var found = MockStore.SubscriptionById(sid);
    if (found is null) return Results.StatusCode(404);
    return found.Value.state == "on_hold"
        ? J(new { subscription = MockStore.Subscription(int.Parse(sid), "active", MockStore.ProProduct(), MockStore.KnownCustomer()) })
        : Errors422("Only subscriptions that are on hold can be resumed.");
});

// 7. Reactivate (PUT)
app.MapMethods("/subscriptions/{sid}/reactivate.json", new[] { "PUT" }, (string sid) =>
{
    var found = MockStore.SubscriptionById(sid);
    if (found is null) return Results.StatusCode(404);
    return found.Value.state == "canceled"
        ? J(new { subscription = MockStore.Subscription(int.Parse(sid), "active", MockStore.ProProduct(), MockStore.KnownCustomer()) })
        : Errors422("Cannot reactivate a subscription that is not canceled.");
});

// 8. Create subscription  (note: 201 Created)
app.MapPost("/subscriptions.json", async (HttpContext ctx) =>
{
    var root = await ReadBody(ctx);
    if (!TryProp(root, "subscription", out var s) || s.ValueKind != JsonValueKind.Object)
        return Errors422("Subscription: cannot be blank.");
    var product = MockStore.ProductByHandle(Str(s, "product_handle"));
    if (product is null) return Errors422("Product: could not be found.");
    var custRef = Str(s, "customer_reference") ?? MockStore.KnownCustomerReference;
    var customer = MockStore.Customer(MockStore.KnownCustomerId, "Ada", "Lovelace", "ada@example.com", custRef);
    return J(new { subscription = MockStore.Subscription(MockStore.NewSubscriptionId, "active", product, customer) }, 201);
});

// 9. Commit plan change (migrations)
app.MapPost("/subscriptions/{sid}/migrations.json", async (HttpContext ctx, string sid) =>
{
    var found = MockStore.SubscriptionById(sid);
    if (found is null) return Results.StatusCode(404);
    var root = await ReadBody(ctx);
    if (!TryProp(root, "migration", out var m) || m.ValueKind != JsonValueKind.Object)
        return Errors422("Migration: cannot be blank.");
    var product = MockStore.ProductByHandle(Str(m, "product_handle"));
    if (product is null) return Errors422("Product: could not be found.");
    return J(new { subscription = MockStore.Subscription(int.Parse(sid), "active", product, MockStore.KnownCustomer()) });
});

// 10. Cancel (DELETE, immediate)
app.MapDelete("/subscriptions/{sid}.json", (string sid) =>
{
    var found = MockStore.SubscriptionById(sid);
    if (found is null) return Results.StatusCode(404);
    return found.Value.state == "canceled"
        ? J(new { error = "The subscription is already canceled" }, 422)
        : J(new { subscription = MockStore.Subscription(int.Parse(sid), "canceled", MockStore.ProProduct(), MockStore.KnownCustomer()) });
});

// 11. Record usage
app.MapPost("/subscriptions/{sid}/components/{cid}/usages.json", async (HttpContext ctx, string sid, string cid) =>
{
    var found = MockStore.SubscriptionById(sid);
    if (found is null) return Results.StatusCode(404);
    if (cid != MockStore.MeteredComponentId.ToString()) return Errors422("Component: could not be found.");
    var root = await ReadBody(ctx);
    if (!TryProp(root, "usage", out var u) || u.ValueKind != JsonValueKind.Object)
        return Errors422("Usage: cannot be blank.");
    object quantity = TryProp(u, "quantity", out var q) && q.ValueKind == JsonValueKind.Number ? q.GetInt64() : 0L;
    return J(new { usage = MockStore.Usage(MockStore.UsageId, quantity, MockStore.MeteredComponentId, int.Parse(sid), Str(u, "memo")) });
});

app.Run();

// ---- mutable mock state (toggled by the gate via /__mock/config) --------------------------------
sealed class MockState
{
    public bool RequireAuth;
    public void Reset() => RequireAuth = false;
}

sealed record MockConfig(bool? RequireAuth, List<FaultRuleDto>? Faults);
sealed record FaultRuleDto(string? PathContains, string? Method, string? Action, int? Times, int? RetryAfter);
