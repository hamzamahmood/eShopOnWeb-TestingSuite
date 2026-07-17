using System.Diagnostics;
using Harness.Core;

namespace Harness.Gate;

public sealed record CheckResult(bool Passed, string Detail);
public sealed record Check(string Id, Func<Task<CheckResult>> Run);

public sealed class GateContext(AppClient app, MockClient mock, Func<string> appLog, Profile profile, OpTable ops)
{
    public AppClient App => app;
    public MockClient Mock => mock;
    public Func<string> AppLog => appLog;
    public Profile Profile => profile;
    public OpTable Ops => ops;
    public string Prefix => profile.App.RoutePrefix;
}

/// <summary>
/// Data-driven encoding of PRODUCTION_READINESS Part A. C1 iterates the op table; the resilience /
/// hygiene / security checks are fixed property TEMPLATES resolved against role assignments
/// (read / readById / write / unknownIdPath) so they run against any integration with no hard-coded op
/// names. Every check is deterministic — status ranges, value-presence, mock call-counts, coarse timing,
/// forbidden-substring hygiene. No LLM judge, no source inspection.
/// </summary>
public static class Checks
{
    static CheckResult P() => new(true, "");
    static CheckResult F(string d) => new(false, d);
    static string Trunc(string s) => s.Length <= 200 ? s : s[..200];

    public static IReadOnlyList<Check> Public(GateContext c)
    {
        string[] leak = c.Profile.Leak.All;
        string? Leak(ApiResponse r) => leak.FirstOrDefault(r.Has);
        string prefix = c.Prefix;

        Task<ApiResponse> Drive(Op op, string? bodyOverride = null)
            => c.App.DriveOp(prefix, op.App, bodyOverride);

        var read = Require(c.Ops, c.Ops.Roles.Read, "roles.read");
        var readById = Require(c.Ops, c.Ops.Roles.ReadById, "roles.readById");
        var write = Require(c.Ops, c.Ops.Roles.Write, "roles.write");
        var unknownPath = prefix + c.Ops.Roles.UnknownIdPath;

        var checks = new List<Check>();

        // ---- C1: happy path per op — value-presence (representation-agnostic) + a genuine upstream call ----
        foreach (var op in c.Ops.Ops)
        {
            var o = op;
            checks.Add(new Check($"C1.{o.Id}", async () =>
            {
                await c.Mock.Reset();
                var r = await Drive(o);
                var up = await c.Mock.Count(o.Upstream.Method, o.Upstream.PathContains);
                var okVals = r.HasAll(o.Gate.MustContain) && o.Gate.MustContainAny.All(r.HasAnyOf);
                return r.Ok && okVals && up >= 1
                    ? P() : F($"status={r.Status} upstream={up} body={Trunc(r.Body)}");
            }));
        }

        // ---- C2: tolerate unknown/extra upstream fields ----
        checks.Add(new Check("C2.extra-fields", async () =>
        {
            await c.Mock.Reset();
            var r = await Drive(readById);
            return r.Ok ? P() : F($"status={r.Status}");
        }));

        // ---- C3: invalid request rejected locally, zero upstream calls ----
        checks.Add(new Check("C3.local-validation", async () =>
        {
            await c.Mock.Reset();
            var r = await Drive(write, write.InvalidBody ?? "{}");
            var calls = await c.Mock.Total();
            return r.Is4xx && calls == 0 ? P() : F($"status={r.Status} upstreamCalls={calls} body={Trunc(r.Body)}");
        }));

        // ---- E1: provider domain error -> defensible 4xx + clean body ----
        checks.Add(new Check("E1.provider-4xx", async () =>
        {
            await c.Mock.Reset();
            var r = await Drive(write, write.DomainErrorBody ?? write.App.Body);
            return r.Is4xx && Leak(r) is null ? P() : F($"status={r.Status} leak={Leak(r)} body={Trunc(r.Body)}");
        }));

        // ---- E2: unknown resource -> 4xx, never 5xx/crash ----
        checks.Add(new Check("E2.unknown-4xx", async () =>
        {
            await c.Mock.Reset();
            var r = await c.App.Get(unknownPath);
            return r.Is4xx ? P() : F($"status={r.Status} body={Trunc(r.Body)}");
        }));

        // ---- E3: no internals leaked across failure bodies ----
        checks.Add(new Check("E3.hygiene", async () =>
        {
            await c.Mock.Reset();
            var bodies = new List<ApiResponse>
            {
                await c.App.Get(unknownPath),
                await Drive(write, write.DomainErrorBody ?? write.App.Body),
            };
            await c.Mock.Fault(read.Upstream.PathContains, null, "reset", 9);
            bodies.Add(await Drive(read));
            var leaks = bodies.Select(Leak).Where(x => x is not null).ToList();
            return leaks.Count == 0 ? P() : F($"leaked: {string.Join(",", leaks)}");
        }));

        // ---- E4: malformed upstream body -> mapped error, no crash/leak ----
        checks.Add(new Check("E4.malformed", async () =>
        {
            await c.Mock.Reset();
            await c.Mock.Fault(readById.Upstream.PathContains, null, "malformed", 1);
            var r = await Drive(readById);
            return !r.Ok && !r.Crashed && Leak(r) is null ? P() : F($"status={r.Status} leak={Leak(r)} body={Trunc(r.Body)}");
        }));

        // ---- R1: transient 5xx on a safe read recovers ----
        checks.Add(new Check("R1.5xx-recovers", async () =>
        {
            await c.Mock.Reset();
            await c.Mock.Fault(read.Upstream.PathContains, null, "status503", 2);
            var r = await Drive(read);
            return r.Ok ? P() : F($"status={r.Status} (did not retry through 2x503) body={Trunc(r.Body)}");
        }));

        // ---- R2: rate-limit (429) recovers ----
        checks.Add(new Check("R2.429-recovers", async () =>
        {
            await c.Mock.Reset();
            await c.Mock.Fault(read.Upstream.PathContains, null, "status429", 1, 1);
            var r = await Drive(read);
            return r.Ok ? P() : F($"status={r.Status} body={Trunc(r.Body)}");
        }));

        // ---- R3: transport fault wrapped, not leaked ----
        checks.Add(new Check("R3.transport-wrapped", async () =>
        {
            await c.Mock.Reset();
            await c.Mock.Fault(read.Upstream.PathContains, null, "reset", 9);
            var r = await Drive(read);
            return r.Is5xx && !r.Crashed && Leak(r) is null ? P() : F($"status={r.Status} leak={Leak(r)} body={Trunc(r.Body)}");
        }));

        // ---- R4: a timeout exists (client never hangs forever) ----
        checks.Add(new Check("R4.timeout-bounded", async () =>
        {
            await c.Mock.Reset();
            await c.Mock.Fault(write.Upstream.PathContains, write.Upstream.Method, "hang", 9, 65);
            var sw = Stopwatch.StartNew();
            var r = await Drive(write);
            sw.Stop();
            return !r.Ok && !r.Crashed && sw.Elapsed.TotalSeconds < 60
                ? P() : F($"status={r.Status} elapsed={sw.Elapsed.TotalSeconds:F1}s body={Trunc(r.Body)}");
        }));

        // ---- R5: a failed write is not duplicated ----
        checks.Add(new Check("R5.no-dup-write", async () =>
        {
            await c.Mock.Reset();
            await c.Mock.Fault(write.Upstream.PathContains, write.Upstream.Method, "status503", 9);
            await Drive(write);
            var posts = await c.Mock.Count(write.Upstream.Method, write.Upstream.PathContains);
            return posts == 1 ? P() : F($"upstream {write.Upstream.Method} {write.Upstream.PathContains} count={posts} (expected exactly 1)");
        }));

        // ---- R6: retries are bounded ----
        checks.Add(new Check("R6.bounded", async () =>
        {
            await c.Mock.Reset();
            await c.Mock.Fault(read.Upstream.PathContains, null, "status503", 99);
            var r = await Drive(read);
            var gets = await c.Mock.Count(read.Upstream.Method, read.Upstream.PathContains);
            return !r.Ok && gets <= 6 ? P() : F($"status={r.Status} upstream count={gets} (expected <=6 and non-2xx)");
        }));

        // ---- S1: the secret(s) never appear in the app's logs ----
        checks.Add(new Check("S1.no-secret-log", async () =>
        {
            await c.Mock.Reset();
            await Drive(read);
            var log = c.AppLog();
            var hit = c.Profile.App.SecretValues.FirstOrDefault(s => log.Contains(s, StringComparison.OrdinalIgnoreCase));
            return hit is null ? P() : F($"secret value '{hit}' found in app log output");
        }));

        // ---- S2: auth is actually applied (mock rejects unauthenticated) ----
        checks.Add(new Check("S2.auth-applied", async () =>
        {
            await c.Mock.Reset();
            await c.Mock.RequireAuth();
            var r = await Drive(read);
            return r.Ok ? P() : F($"status={r.Status} (app's upstream call rejected for missing/invalid auth)");
        }));

        return checks;
    }

    // ---- Holdout: same property classes, different concrete instances (never shown to the builder) ----
    public static IReadOnlyList<Check> Holdout(GateContext c)
    {
        string[] leak = c.Profile.Leak.All;
        string? Leak(ApiResponse r) => leak.FirstOrDefault(r.Has);
        string prefix = c.Prefix;
        Task<ApiResponse> Drive(Op op) => c.App.DriveOp(prefix, op.App);

        var h = c.Ops.Holdout;
        var read = Require(c.Ops, h.Read, "holdout.read");
        var readById = Require(c.Ops, h.ReadById, "holdout.readById");
        var write = Require(c.Ops, h.Write, "holdout.write");
        var write2 = Require(c.Ops, h.Write2, "holdout.write2");

        return new List<Check>
        {
            new("H.R1.recover", async () =>
            {
                await c.Mock.Reset();
                await c.Mock.Fault(read.Upstream.PathContains, null, "status503", 2);
                var r = await Drive(read);
                return r.Ok && r.HasAll(read.Gate.MustContain) ? P() : F($"status={r.Status} body={Trunc(r.Body)}");
            }),
            new("H.R3.read-reset", async () =>
            {
                await c.Mock.Reset();
                await c.Mock.Fault(readById.Upstream.PathContains, null, "reset", 9);
                var r = await Drive(readById);
                return r.Is5xx && !r.Crashed && Leak(r) is null ? P() : F($"status={r.Status} leak={Leak(r)} body={Trunc(r.Body)}");
            }),
            new("H.E4.write-malformed", async () =>
            {
                await c.Mock.Reset();
                await c.Mock.Fault(write.Upstream.PathContains, write.Upstream.Method, "malformed", 1);
                var r = await Drive(write);
                return !r.Ok && !r.Crashed && Leak(r) is null ? P() : F($"status={r.Status} leak={Leak(r)} body={Trunc(r.Body)}");
            }),
            new("H.R5.write2-no-dup", async () =>
            {
                await c.Mock.Reset();
                await c.Mock.Fault(write2.Upstream.PathContains, write2.Upstream.Method, "status503", 9);
                await Drive(write2);
                var posts = await c.Mock.Count(write2.Upstream.Method, write2.Upstream.PathContains);
                return posts == 1 ? P() : F($"upstream count={posts} (expected exactly 1)");
            }),
            new("H.S1.secret2-not-logged", async () =>
            {
                await c.Mock.Reset();
                await Drive(read);
                var log = c.AppLog();
                return !log.Contains(h.SecretValue, StringComparison.OrdinalIgnoreCase) ? P() : F($"secret '{h.SecretValue}' leaked in app log");
            }),
        };
    }

    static Op Require(OpTable t, string id, string role)
        => t.ById(id) ?? throw new InvalidOperationException($"{role} references unknown op id '{id}'");
}
