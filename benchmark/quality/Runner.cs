namespace Quality;

public sealed record CheckResult(string Id, bool Pass, string Detail);
public sealed record D1Report(int Pass, int Total, double Rate, IReadOnlyList<CheckResult> Checks);

public sealed record Cell(string Op, string Drift, string Class, int Status, string Detail);
public sealed record D2Report(double Survival, int Correct, int Graceful, int Broken, int SilentWrong, IReadOnlyList<Cell> Cells);

public static class Runner
{
    // failure-body markers that make any response BROKEN regardless of status
    static readonly string[] Leak =
    {
        "System.", "Microsoft.", "   at ", ".cs:line", "StackTrace", "Traceback",
        "HttpRequestException", "NullReference", "test-api-key", "price_in_cents",
    };

    static bool HasLeak(ApiResponse r) => Leak.Any(m => r.Has(m));

    // The price magnitude must be surfaced faithfully in EITHER cents or dollars form. Which representation
    // the arm picks is a defensible design choice (the Stage-1 gate deliberately did not assert an encoding);
    // the correctness property is that the price is present and not silently dropped/corrupted. Under a
    // price-field-rename drift the value disappears in BOTH forms ⇒ correctly flagged SILENT-WRONG.
    static bool PricePresent(ApiResponse r, double dollars)
    {
        var near = new Func<double, double, bool>((n, t) => Math.Abs(n - t) <= Math.Max(0.5, t * 0.002));
        return AppClient.Numbers(r.Body).Any(n => near(n, dollars) || near(n, dollars * 100));
    }

    // ---- D1: correctness depth ------------------------------------------------------------------
    public static async Task<D1Report> RunDeep(Harness h)
    {
        var checks = new List<CheckResult>();
        foreach (var op in Ops.All)
        {
            await h.Mock.Reset();
            var r = await h.App.Call(op.Method, op.AppPath, op.Body);

            // values check: 2xx + all required values present + no leak
            var missing = op.MustContain.Where(v => !r.Has(v)).ToArray();
            var valuesPass = r.Ok && missing.Length == 0 && !HasLeak(r);
            checks.Add(new CheckResult($"{op.Id}.values", valuesPass,
                valuesPass ? "" : $"status={r.Status} missing=[{string.Join(",", missing)}] leak={HasLeak(r)}"));

            // price check: the price magnitude must be surfaced (cents or dollars), not dropped/corrupted
            if (op.ExpectDollars is double d)
            {
                var pricePass = r.Ok && PricePresent(r, d);
                checks.Add(new CheckResult($"{op.Id}.price", pricePass,
                    pricePass ? "" : $"no price≈{d}/{d * 100} in [{string.Join(",", AppClient.Numbers(r.Body).Take(12))}]"));
            }
        }

        // synthetic: unknown id → a clean 4xx, never 5xx/crash
        await h.Mock.Reset();
        var unk = await h.App.Get("/api/billing/subscriptions/999999");
        var unkPass = unk.Is4xx && !HasLeak(unk);
        checks.Add(new CheckResult("unknown-id.4xx", unkPass, unkPass ? "" : $"status={unk.Status} leak={HasLeak(unk)}"));

        var pass = checks.Count(c => c.Pass);
        return new D1Report(pass, checks.Count, checks.Count == 0 ? 0 : (double)pass / checks.Count, checks);
    }

    // ---- D2: API-drift resilience ---------------------------------------------------------------
    public static async Task<D2Report> RunDrift(Harness h)
    {
        var cells = new List<Cell>();
        foreach (var op in Ops.All)
            foreach (var dc in op.Drifts)
            {
                await h.Mock.Reset();
                await h.Mock.Drift(op.Upstream, op.Method == "GET" || dc.Profile == "additive" ? null : op.Method, dc.Profile, dc.Field, dc.To);
                var r = await h.App.Call(op.Method, op.AppPath, op.Body);
                var (cls, detail) = Classify(op, dc, r);
                cells.Add(new Cell(op.Id, dc.Label, cls, r.Status, detail));
            }

        int c = cells.Count(x => x.Class == "CORRECT"),
            g = cells.Count(x => x.Class == "GRACEFUL"),
            b = cells.Count(x => x.Class == "BROKEN"),
            s = cells.Count(x => x.Class == "SILENT-WRONG");
        var survival = cells.Count == 0 ? 0 : (c * 1.0 + g * 0.5) / cells.Count;
        return new D2Report(survival, c, g, b, s, cells);
    }

    static (string, string) Classify(QOp op, DriftCase dc, ApiResponse r)
    {
        if (r.Crashed) return ("BROKEN", "no response (crash/hang/refused)");
        if (r.Is5xx || HasLeak(r)) return ("BROKEN", $"status={r.Status} leak={HasLeak(r)}");
        if (r.Is4xx) return ("GRACEFUL", $"clean {r.Status}");   // rejected the drift without crashing/leaking

        // 2xx:
        switch (dc.Check)
        {
            case DriftCheck.NewEnum:
                var exp = dc.Expect ?? op.MustContain;
                if (exp.All(r.Has)) return ("CORRECT", "surfaced new enum value");
                return exp.Length > 0 && r.Has(exp[0])
                    ? ("SILENT-WRONG", "2xx but dropped the drifted enum value")
                    : ("BROKEN", "2xx but core data absent");
            case DriftCheck.Units:
                return op.ExpectDollars is double d && PricePresent(r, d)
                    ? ("CORRECT", "price still surfaced")
                    : ("SILENT-WRONG", "2xx but price silently dropped");
            default: // Values
                var want = dc.Expect ?? op.MustContain;
                var miss = want.Where(v => !r.Has(v)).ToArray();
                return miss.Length == 0
                    ? ("CORRECT", "all required values present")
                    : ("SILENT-WRONG", $"2xx but missing [{string.Join(",", miss)}]");
        }
    }
}
