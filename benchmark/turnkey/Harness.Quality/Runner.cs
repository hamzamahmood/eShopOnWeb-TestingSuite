using Harness.Core;

namespace Harness.Quality;

public sealed record DeepCheck(string Id, bool Pass, string Detail);
public sealed record D1Report(int Pass, int Total, double Rate, IReadOnlyList<DeepCheck> Checks);

public sealed record Cell(string Op, string Drift, string Class, int Status, string Detail);
/// <summary>Two orthogonal lenses on drift + the raw confusion matrix (the primary artifact).
/// Resilience = (CORRECT + 0.5·GRACEFUL)/N; Safety = (N − SILENT-WRONG)/N.</summary>
public sealed record D2Report(double Resilience, double Safety, int Correct, int Graceful, int Broken, int SilentWrong, IReadOnlyList<Cell> Cells);

/// <summary>D1 deep-correctness + D2 drift-resilience, driven by the profile's op table. The produced
/// integration's code is never touched — D2 replays it against a provider whose JSON has been mutated,
/// one isolated drift cell at a time. Ports benchmark/quality/Runner.cs onto the generic op model.</summary>
public static class Runner
{
    static bool HasLeak(ApiResponse r, string[] leak) => leak.Any(r.Has);

    static bool PricePresent(ApiResponse r, double dollars)
    {
        bool Near(double n, double t) => Math.Abs(n - t) <= Math.Max(0.5, t * 0.002);
        return AppClient.Numbers(r.Body).Any(n => Near(n, dollars) || Near(n, dollars * 100));
    }

    /// <summary>Detect task size: probe up to 3 GET ops at the table's max scope; any non-404 ⇒ that
    /// scope (the routes exist), else the next lower scope. Probing several guards against a single
    /// broken extended endpoint silently shrinking the test set.</summary>
    public static async Task<int> DetectScope(AppClient app, string prefix, OpTable ops)
    {
        var max = ops.Ops.Select(o => o.Scope).DefaultIfEmpty(0).Max();
        if (max == 0) return 0;
        foreach (var op in ops.Ops.Where(o => o.Scope == max && o.App.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)).Take(3))
            if ((await app.Get(prefix + op.App.Path)).Status != 404) return max;
        return ops.Ops.Where(o => o.Scope < max).Select(o => o.Scope).DefaultIfEmpty(0).Max();
    }

    static IEnumerable<Op> DeepOps(OpTable ops, int scope) => ops.Ops.Where(o => o.Deep is not null && o.Scope <= scope);

    // ---- D1: correctness depth ------------------------------------------------------------------
    public static async Task<D1Report> RunDeep(AppClient app, MockClient mock, string prefix, OpTable ops, string[] leak, int scope)
    {
        var checks = new List<DeepCheck>();
        foreach (var op in DeepOps(ops, scope))
        {
            var d = op.DeepOrGate;
            await mock.Reset();
            var r = await app.Call(op.App.Method, prefix + op.App.Path, op.App.Body);

            var missing = d.MustContain.Where(v => !r.Has(v)).ToArray();
            var anyOk = d.MustContainAny.All(r.HasAnyOf);
            var valuesPass = r.Ok && missing.Length == 0 && anyOk && !HasLeak(r, leak);
            checks.Add(new DeepCheck($"{op.Id}.values", valuesPass,
                valuesPass ? "" : $"status={r.Status} missing=[{string.Join(",", missing)}] anyOk={anyOk} leak={HasLeak(r, leak)}"));

            if (d.ExpectDollars is double dollars)
            {
                var pricePass = r.Ok && PricePresent(r, dollars);
                checks.Add(new DeepCheck($"{op.Id}.price", pricePass,
                    pricePass ? "" : $"no price≈{dollars}/{dollars * 100} in [{string.Join(",", AppClient.Numbers(r.Body).Take(12))}]"));
            }
        }

        await mock.Reset();
        var unk = await app.Get(prefix + ops.Roles.UnknownIdPath);
        var unkPass = unk.Is4xx && !HasLeak(unk, leak);
        checks.Add(new DeepCheck("unknown-id.4xx", unkPass, unkPass ? "" : $"status={unk.Status} leak={HasLeak(unk, leak)}"));

        var pass = checks.Count(c => c.Pass);
        return new D1Report(pass, checks.Count, checks.Count == 0 ? 0 : (double)pass / checks.Count, checks);
    }

    // ---- D2: API-drift resilience ---------------------------------------------------------------
    public static async Task<D2Report> RunDrift(AppClient app, MockClient mock, string prefix, OpTable ops, string[] leak, int scope)
    {
        var cells = new List<Cell>();
        foreach (var op in DeepOps(ops, scope))
            foreach (var dc in op.Drifts)
            {
                await mock.Reset();
                var driftMethod = (dc.Profile.Equals("additive", StringComparison.OrdinalIgnoreCase)
                                   || op.Upstream.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                                  ? null : op.Upstream.Method;
                await mock.Drift(op.Upstream.PathContains, driftMethod, dc.Profile, dc.Field, dc.To);
                var r = await app.Call(op.App.Method, prefix + op.App.Path, op.App.Body);
                var (cls, detail) = Classify(op, dc, r, leak);
                cells.Add(new Cell(op.Id, dc.Label, cls, r.Status, detail));
            }

        int c = cells.Count(x => x.Class == "CORRECT"),
            g = cells.Count(x => x.Class == "GRACEFUL"),
            b = cells.Count(x => x.Class == "BROKEN"),
            s = cells.Count(x => x.Class == "SILENT-WRONG");
        int n = cells.Count == 0 ? 1 : cells.Count;
        return new D2Report((c + 0.5 * g) / n, (n - s) * 1.0 / n, c, g, b, s, cells);
    }

    static (string, string) Classify(Op op, DriftCaseSpec dc, ApiResponse r, string[] leak)
    {
        if (r.Crashed) return ("BROKEN", "no response (crash/hang/refused)");
        if (r.Is5xx || HasLeak(r, leak)) return ("BROKEN", $"status={r.Status} leak={HasLeak(r, leak)}");
        if (r.Is4xx) return ("GRACEFUL", $"clean {r.Status}");

        switch (dc.Check.ToLowerInvariant())
        {
            case "newenum":
                var exp = dc.Expect ?? op.DeepOrGate.MustContain;
                if (exp.All(r.Has)) return ("CORRECT", "surfaced new enum value");
                return exp.Length > 0 && r.Has(exp[0])
                    ? ("SILENT-WRONG", "2xx but dropped the drifted enum value")
                    : ("BROKEN", "2xx but core data absent");
            case "units":
                return op.DeepOrGate.ExpectDollars is double d && PricePresent(r, d)
                    ? ("CORRECT", "price still surfaced")
                    : ("SILENT-WRONG", "2xx but price silently dropped");
            default: // values
                var want = dc.Expect ?? op.DeepOrGate.MustContain;
                var miss = want.Where(v => !r.Has(v)).ToArray();
                return miss.Length == 0
                    ? ("CORRECT", "all required values present")
                    : ("SILENT-WRONG", $"2xx but missing [{string.Join(",", miss)}]");
        }
    }
}
