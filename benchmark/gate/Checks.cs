using System.Diagnostics;

namespace Gate;

public sealed record CheckResult(bool Passed, string Detail);
public sealed record Check(string Id, Func<Task<CheckResult>> Run);

public sealed class GateContext(AppClient app, MockClient mock, Func<string> appLog)
{
    public AppClient App => app;
    public MockClient Mock => mock;
    public Func<string> AppLog => appLog;
}

/// <summary>
/// The executable encoding of PRODUCTION_READINESS §6 (public) and §7 (holdout). Every check is
/// deterministic: status ranges, value-presence (field-name-agnostic), mock request-counts,
/// coarse timing, and forbidden-substring hygiene. No LLM judge, no code inspection.
/// </summary>
public static class Checks
{
    // fixtures (mirror the mock)
    const string CustKnown = "cust_known";
    const long   CustKnownId = 900001;
    const long   SubActive = 950001, SubOnHold = 950002, SubCanceled = 950003;
    const string Pro = "pro-plan", Basic = "basic-plan";

    static readonly string[] Forbidden =
    {
        "System.", "Microsoft.", "   at ", ".cs:line", "StackTrace", "Traceback",
        "test-api-key", "price_in_cents", "product_family", "HttpRequestException", "NullReference",
    };

    static bool Ok(ApiResponse r)   => r.Status is >= 200 and < 300;
    static bool Is4xx(ApiResponse r)=> r.Status is >= 400 and < 500;
    static bool Is5xx(ApiResponse r)=> r.Status is >= 500 and < 600;
    static bool Has(ApiResponse r, params string[] vals) => vals.All(v => r.Body.Contains(v, StringComparison.OrdinalIgnoreCase));
    static string? Leak(ApiResponse r) => Forbidden.FirstOrDefault(f => r.Body.Contains(f, StringComparison.OrdinalIgnoreCase));
    static string Trunc(string s) => s.Length <= 180 ? s : s[..180];
    static CheckResult Pass() => new(true, "");
    static CheckResult P() => new(true, "");
    static CheckResult F(string d) => new(false, d);

    static string Faults(string body) => "{\"faults\":[" + body + "]}";

    public static IReadOnlyList<Check> Public(GateContext c) => new List<Check>
    {
        // ---- C1: happy path per op (value-presence, field-name-agnostic) ----
        new("C1.plans",         async () => { await c.Mock.Reset(); var r = await c.App.Get("/api/billing/plans");
            return Ok(r) && Has(r, "700001", "29900") ? P() : F($"status={r.Status} body={Trunc(r.Body)}"); }),
        new("C1.find-or-create", async () => { await c.Mock.Reset(); var r = await c.App.Post("/api/billing/customers", Cust(CustKnown));
            return Ok(r) && Has(r, "900001") ? P() : F($"status={r.Status} body={Trunc(r.Body)}"); }),
        new("C1.list-subs",     async () => { await c.Mock.Reset(); var r = await c.App.Get($"/api/billing/customers/{CustKnownId}/subscriptions");
            return Ok(r) && Has(r, "950001") ? P() : F($"status={r.Status} body={Trunc(r.Body)}"); }),
        new("C1.read-sub",      async () => { await c.Mock.Reset(); var r = await c.App.Get($"/api/billing/subscriptions/{SubActive}");
            return Ok(r) && Has(r, "active") ? P() : F($"status={r.Status} body={Trunc(r.Body)}"); }),
        new("C1.pause",         async () => { await c.Mock.Reset(); var r = await c.App.Post($"/api/billing/subscriptions/{SubActive}/pause");
            return Ok(r) && Has(r, "on_hold") ? P() : F($"status={r.Status} body={Trunc(r.Body)}"); }),
        new("C1.resume",        async () => { await c.Mock.Reset(); var r = await c.App.Post($"/api/billing/subscriptions/{SubOnHold}/resume");
            return Ok(r) && Has(r, "active") ? P() : F($"status={r.Status} body={Trunc(r.Body)}"); }),
        new("C1.reactivate",    async () => { await c.Mock.Reset(); var r = await c.App.Post($"/api/billing/subscriptions/{SubCanceled}/reactivate");
            return Ok(r) && Has(r, "active") ? P() : F($"status={r.Status} body={Trunc(r.Body)}"); }),
        new("C1.create-sub",    async () => { await c.Mock.Reset(); var r = await c.App.Post("/api/billing/subscriptions", Sub(CustKnown, Pro));
            return Ok(r) && Has(r, "950010") ? P() : F($"status={r.Status} body={Trunc(r.Body)}"); }),
        new("C1.plan-change",   async () => { await c.Mock.Reset(); var r = await c.App.Post($"/api/billing/subscriptions/{SubActive}/plan-change", $"{{\"productHandle\":\"{Basic}\"}}");
            return Ok(r) && Has(r, Basic) ? P() : F($"status={r.Status} body={Trunc(r.Body)}"); }),
        new("C1.cancel",        async () => { await c.Mock.Reset(); var r = await c.App.Delete($"/api/billing/subscriptions/{SubActive}");
            return Ok(r) && Has(r, "canceled") ? P() : F($"status={r.Status} body={Trunc(r.Body)}"); }),
        new("C1.usage",         async () => { await c.Mock.Reset(); var r = await c.App.Post($"/api/billing/subscriptions/{SubActive}/usage", "{\"quantity\":5,\"memo\":\"m\"}");
            return Ok(r) && Has(r, "990001") ? P() : F($"status={r.Status} body={Trunc(r.Body)}"); }),

        // ---- C2: tolerate unknown/extra upstream fields (mock returns many the app won't model) ----
        new("C2.extra-fields",  async () => { await c.Mock.Reset(); var r = await c.App.Get($"/api/billing/subscriptions/{SubActive}");
            return Ok(r) ? P() : F($"status={r.Status}"); }),

        // ---- C3: invalid request rejected locally, zero upstream calls ----
        new("C3.local-validation", async () => { await c.Mock.Reset(); var r = await c.App.Post("/api/billing/subscriptions", "{}");
            var calls = await c.Mock.Total();
            return Is4xx(r) && calls == 0 ? P() : F($"status={r.Status} upstreamCalls={calls} body={Trunc(r.Body)}"); }),

        // ---- E1: provider domain error -> defensible 4xx + clean body ----
        new("E1.provider-4xx",  async () => { await c.Mock.Reset(); var r = await c.App.Post("/api/billing/subscriptions", Sub(CustKnown, "does-not-exist"));
            return Is4xx(r) && Leak(r) is null ? P() : F($"status={r.Status} leak={Leak(r)} body={Trunc(r.Body)}"); }),

        // ---- E2: unknown resource -> 4xx, never 5xx/crash ----
        new("E2.unknown-4xx",   async () => { await c.Mock.Reset(); var r = await c.App.Get("/api/billing/subscriptions/999");
            return Is4xx(r) ? P() : F($"status={r.Status} body={Trunc(r.Body)}"); }),

        // ---- E3: no internals leaked across failure bodies ----
        new("E3.hygiene",       async () => {
            await c.Mock.Reset();
            var bodies = new List<ApiResponse> {
                await c.App.Get("/api/billing/subscriptions/999"),
                await c.App.Post("/api/billing/subscriptions", Sub(CustKnown, "does-not-exist")),
            };
            await c.Mock.Config(Faults("{\"pathContains\":\"products.json\",\"action\":\"reset\",\"times\":9}"));
            bodies.Add(await c.App.Get("/api/billing/plans"));
            var leaks = bodies.Select(Leak).Where(x => x is not null).ToList();
            return leaks.Count == 0 ? P() : F($"leaked: {string.Join(",", leaks)}"); }),

        // ---- E4: malformed upstream body -> mapped error, no crash/leak ----
        new("E4.malformed",     async () => { await c.Mock.Reset();
            await c.Mock.Config(Faults($"{{\"pathContains\":\"/subscriptions/{SubActive}.json\",\"action\":\"malformed\",\"times\":1}}"));
            var r = await c.App.Get($"/api/billing/subscriptions/{SubActive}");
            return !Ok(r) && r.Status != 0 && Leak(r) is null ? P() : F($"status={r.Status} leak={Leak(r)} body={Trunc(r.Body)}"); }),

        // ---- R1: transient 5xx on a safe GET recovers ----
        new("R1.5xx-recovers",  async () => { await c.Mock.Reset();
            await c.Mock.Config(Faults("{\"pathContains\":\"products.json\",\"action\":\"status503\",\"times\":2}"));
            var r = await c.App.Get("/api/billing/plans");
            return Ok(r) ? P() : F($"status={r.Status} (did not retry through 2x503) body={Trunc(r.Body)}"); }),

        // ---- R2: rate-limit (429) recovers ----
        new("R2.429-recovers",  async () => { await c.Mock.Reset();
            await c.Mock.Config(Faults("{\"pathContains\":\"products.json\",\"action\":\"status429\",\"times\":1,\"retryAfter\":1}"));
            var r = await c.App.Get("/api/billing/plans");
            return Ok(r) ? P() : F($"status={r.Status} body={Trunc(r.Body)}"); }),

        // ---- R3: transport fault wrapped, not leaked ----
        new("R3.transport-wrapped", async () => { await c.Mock.Reset();
            await c.Mock.Config(Faults("{\"pathContains\":\"products.json\",\"action\":\"reset\",\"times\":9}"));
            var r = await c.App.Get("/api/billing/plans");
            return Is5xx(r) && r.Status != 0 && Leak(r) is null ? P() : F($"status={r.Status} leak={Leak(r)} body={Trunc(r.Body)}"); }),

        // ---- R4: a timeout exists (client never hangs forever) ----
        new("R4.timeout-bounded", async () => { await c.Mock.Reset();
            await c.Mock.Config(Faults("{\"pathContains\":\"/subscriptions.json\",\"method\":\"POST\",\"action\":\"hang\",\"times\":9,\"retryAfter\":65}"));
            var sw = Stopwatch.StartNew(); var r = await c.App.Post("/api/billing/subscriptions", Sub(CustKnown, Pro)); sw.Stop();
            return !Ok(r) && r.Status != 0 && sw.Elapsed.TotalSeconds < 60 ? P() : F($"status={r.Status} elapsed={sw.Elapsed.TotalSeconds:F1}s body={Trunc(r.Body)}"); }),

        // ---- R5: a failed write is not duplicated ----
        new("R5.no-dup-write",  async () => { await c.Mock.Reset();
            await c.Mock.Config(Faults("{\"pathContains\":\"/subscriptions.json\",\"method\":\"POST\",\"action\":\"status503\",\"times\":9}"));
            await c.App.Post("/api/billing/subscriptions", Sub(CustKnown, Pro));
            var posts = await c.Mock.Count("POST", "/subscriptions.json");
            return posts == 1 ? P() : F($"upstream POST /subscriptions.json count={posts} (expected exactly 1)"); }),

        // ---- R6: retries are bounded ----
        new("R6.bounded",       async () => { await c.Mock.Reset();
            await c.Mock.Config(Faults("{\"pathContains\":\"products.json\",\"action\":\"status503\",\"times\":99}"));
            var r = await c.App.Get("/api/billing/plans");
            var gets = await c.Mock.Count("GET", "products.json");
            return !Ok(r) && gets <= 6 ? P() : F($"status={r.Status} upstream GET count={gets} (expected <=6 and a non-2xx)"); }),

        // ---- S1: the secret never appears in the app's logs ----
        new("S1.no-secret-log", async () => { await c.Mock.Reset();
            await c.App.Get("/api/billing/plans");
            var log = c.AppLog();
            return !log.Contains("test-api-key") ? P() : F("API key found in app log output"); }),

        // ---- S2: auth is actually applied (mock rejects unauthenticated) ----
        new("S2.auth-applied",  async () => { await c.Mock.Reset();
            await c.Mock.Config("{\"requireAuth\":true}");
            var r = await c.App.Get("/api/billing/plans");
            return Ok(r) ? P() : F($"status={r.Status} (app's upstream call was rejected for missing/invalid auth)"); }),
    };

    // ---- Holdout: same property classes, different concrete instances (never shown to the agent) ----
    public static IReadOnlyList<Check> Holdout(GateContext c) => new List<Check>
    {
        // R1 on the lookup GET (not products)
        new("H.R1.lookup-503",  async () => { await c.Mock.Reset();
            await c.Mock.Config(Faults("{\"pathContains\":\"lookup.json\",\"action\":\"status503\",\"times\":2}"));
            var r = await c.App.Post("/api/billing/customers", Cust(CustKnown));
            return Ok(r) && Has(r, "900001") ? P() : F($"status={r.Status} body={Trunc(r.Body)}"); }),
        // R3 transport reset on a READ op (not products)
        new("H.R3.read-reset",  async () => { await c.Mock.Reset();
            await c.Mock.Config(Faults($"{{\"pathContains\":\"/subscriptions/{SubActive}.json\",\"action\":\"reset\",\"times\":9}}"));
            var r = await c.App.Get($"/api/billing/subscriptions/{SubActive}");
            return Is5xx(r) && r.Status != 0 && Leak(r) is null ? P() : F($"status={r.Status} leak={Leak(r)} body={Trunc(r.Body)}"); }),
        // E4 malformed on a different op (create)
        new("H.E4.create-malformed", async () => { await c.Mock.Reset();
            await c.Mock.Config(Faults("{\"pathContains\":\"/subscriptions.json\",\"method\":\"POST\",\"action\":\"malformed\",\"times\":1}"));
            var r = await c.App.Post("/api/billing/subscriptions", Sub(CustKnown, Pro));
            return !Ok(r) && r.Status != 0 && Leak(r) is null ? P() : F($"status={r.Status} leak={Leak(r)} body={Trunc(r.Body)}"); }),
        // R5 no-dup on a different write (record usage)
        new("H.R5.usage-no-dup", async () => { await c.Mock.Reset();
            await c.Mock.Config(Faults("{\"pathContains\":\"/usages.json\",\"method\":\"POST\",\"action\":\"status503\",\"times\":9}"));
            await c.App.Post($"/api/billing/subscriptions/{SubActive}/usage", "{\"quantity\":3}");
            var posts = await c.Mock.Count("POST", "/usages.json");
            return posts == 1 ? P() : F($"upstream POST usages count={posts} (expected exactly 1)"); }),
        // S1 for the subdomain, not the api key
        new("H.S1.no-subdomain-log", async () => { await c.Mock.Reset();
            await c.App.Get("/api/billing/plans");
            var log = c.AppLog();
            return !log.Contains("Subdomain: acme") && !System.Text.RegularExpressions.Regex.IsMatch(log, "\\bacme\\b") ? P() : F("subdomain leaked in app log"); }),
    };

    static string Cust(string reference) => $"{{\"reference\":\"{reference}\",\"firstName\":\"A\",\"lastName\":\"B\",\"email\":\"a@b.com\"}}";
    static string Sub(string custRef, string handle) => $"{{\"customerReference\":\"{custRef}\",\"productHandle\":\"{handle}\"}}";
}
