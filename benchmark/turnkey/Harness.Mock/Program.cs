using System.Text;
using System.Text.Json;
using Harness.Core;
using Harness.Mock;

// ---- args + contract load -----------------------------------------------------------------------
string? Arg(string n) { for (var i = 0; i < args.Length - 1; i++) if (args[i] == n) return args[i + 1]; return null; }
var contractPath = Arg("--contract") ?? Environment.GetEnvironmentVariable("CONTRACT")
    ?? throw new ArgumentException("missing --contract <path>");
var contract = Json.Load<Contract>(contractPath);
// Which request header(s) mark a call as authenticated (S2). Defaults to Authorization; a provider that
// keys auth off a custom header declares it in the contract (e.g. "api_key") so S2 works for it too.
var authHeaders = contract.AuthHeaders is { Length: > 0 } ah ? ah : new[] { "Authorization" };

var builder = WebApplication.CreateBuilder(args);
var listenUrl = Environment.GetEnvironmentVariable("MOCK_URL") ?? "http://localhost:8080";
builder.WebHost.UseUrls(listenUrl);
builder.Logging.ClearProviders();                       // keep the mock's own logs out of the app-log scrape
var app = builder.Build();

var recorder = new Recorder();
var state = new MockState();
var faults = new FaultEngine();
var drift = new DriftEngine();
var webJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var recJson = new JsonSerializerOptions { PropertyNamingPolicy = null };   // PascalCase — MockClient reads Method/Path

// ---- recording + fault-injection + optional auth-check middleware --------------------------------
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
    var hasAuth = authHeaders.Any(h => !string.IsNullOrWhiteSpace(ctx.Request.Headers[h].ToString()));
    recorder.Record(ctx.Request.Method, path, ctx.Request.QueryString.Value ?? "", hasAuth, body);

    // fault injection runs AFTER recording so R5/R6 counts still see the request.
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
                ctx.Response.StatusCode = 200; ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync("{ \"data\": { not valid json ][");
                return;
            case "hang":
                try { await Task.Delay(TimeSpan.FromSeconds(f.RetryAfter > 0 ? f.RetryAfter : 65), ctx.RequestAborted); } catch { }
                return;
            case "reset":
                ctx.Abort();
                return;
            // ---- P7 error-shape drift: provider error bodies of unexpected shapes (quality D2 only) ----
            case "errmap":     // field-map error object instead of a string[]
                await Results.Json(new { errors = new { @base = new[] { "unprocessable" }, field = new[] { "is invalid" } }, error = "unprocessable" }, webJson, statusCode: 422).ExecuteAsync(ctx);
                return;
            case "errstring":  // a single bare JSON string as the whole error body
                ctx.Response.StatusCode = 422; ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync("\"a plain string error\"");
                return;
            case "htmlerror":  // non-JSON (HTML) error body, e.g. a gateway/proxy page
                ctx.Response.StatusCode = 500; ctx.Response.ContentType = "text/html";
                await ctx.Response.WriteAsync("<html><head><title>500</title></head><body>Internal Server Error</body></html>");
                return;
            case "status409":
                await Results.Json(new { errors = new[] { "conflict" } }, webJson, statusCode: 409).ExecuteAsync(ctx);
                return;
        }
    }

    if (state.RequireAuth && !hasAuth)
    {
        await Results.Json(new { error = "authentication required" }, webJson, statusCode: 401).ExecuteAsync(ctx);
        return;
    }
    await next();
});

// ---- drift response mutation (quality D2). No-op unless a rule is installed. ----------------------
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    if (!drift.Any || path.StartsWith("/__mock", StringComparison.OrdinalIgnoreCase)) { await next(); return; }
    var rule = drift.Match(ctx.Request.Method, path);
    if (rule is null) { await next(); return; }

    var original = ctx.Response.Body;
    using var buffer = new MemoryStream();
    ctx.Response.Body = buffer;
    try
    {
        await next();
        buffer.Position = 0;
        var isJson = ctx.Response.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) ?? false;
        if (ctx.Response.StatusCode is >= 200 and < 300 && isJson)
        {
            var b = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();
            var mutated = Encoding.UTF8.GetBytes(DriftEngine.Apply(rule, b));
            ctx.Response.Body = original;
            ctx.Response.ContentLength = mutated.Length;
            await ctx.Response.Body.WriteAsync(mutated);
        }
        else
        {
            ctx.Response.Body = original;
            await buffer.CopyToAsync(original);
        }
    }
    finally { ctx.Response.Body = original; }
});

// ---- control plane (never part of the provider contract; the integration never calls these) ------
app.MapGet("/__mock/health", () => Results.Ok(new { ok = true }));
app.MapGet("/__mock/recordings", () => Results.Json(new { records = recorder.Snapshot() }, recJson));
app.MapPost("/__mock/reset", () => { recorder.Reset(); state.Reset(); faults.Reset(); drift.Reset(); return Results.Ok(new { reset = true }); });
app.MapPost("/__mock/config", async (HttpContext ctx) =>
{
    var cfg = await JsonSerializer.DeserializeAsync<MockConfig>(ctx.Request.Body, webJson);
    if (cfg?.RequireAuth is bool ra) state.RequireAuth = ra;
    if (cfg?.Faults is not null)
        faults.SetRules(cfg.Faults.Select(d => new FaultRule(d.PathContains, d.Method, d.Action ?? "status503", d.Times ?? 1, d.RetryAfter ?? 0)));
    if (cfg?.Drift is not null)
        drift.SetRules(cfg.Drift.Select(d => new DriftRule(d.PathContains, d.Method, d.Profile ?? "additive", d.Field, d.To)));
    return Results.Ok(new { require_auth = state.RequireAuth, faults = cfg?.Faults?.Count ?? 0, drift = cfg?.Drift?.Count ?? 0 });
});

// ---- declarative provider routes -----------------------------------------------------------------
foreach (var routeDef in contract.Routes)
{
    var route = routeDef;   // capture per-iteration
    app.MapMethods(route.Path, new[] { route.Method }, async (HttpContext ctx) =>
    {
        var pathParams = ctx.Request.RouteValues.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString());
        var query = ctx.Request.Query.ToDictionary(kv => kv.Key, kv => (string?)kv.Value.ToString());
        var body = await ReadBodyElement(ctx);

        var rendered = Dispatcher.Resolve(route, contract, pathParams, query, body);
        ctx.Response.StatusCode = rendered.Status;
        ctx.Response.ContentType = rendered.ContentType;
        if (rendered.Body.Length > 0) await ctx.Response.WriteAsync(rendered.Body);
    });
}

app.Run();

static async Task<JsonElement?> ReadBodyElement(HttpContext ctx)
{
    ctx.Request.EnableBuffering();
    ctx.Request.Body.Position = 0;
    using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
    var text = await reader.ReadToEndAsync();
    ctx.Request.Body.Position = 0;
    if (string.IsNullOrWhiteSpace(text)) return null;
    try { using var doc = JsonDocument.Parse(text); return doc.RootElement.Clone(); }
    catch { return null; }
}

// ---- control-plane DTOs --------------------------------------------------------------------------
sealed record MockConfig(bool? RequireAuth, List<FaultRuleDto>? Faults, List<DriftRuleDto>? Drift);
sealed record FaultRuleDto(string? PathContains, string? Method, string? Action, int? Times, int? RetryAfter);
sealed record DriftRuleDto(string? PathContains, string? Method, string? Profile, string? Field, string? To);
