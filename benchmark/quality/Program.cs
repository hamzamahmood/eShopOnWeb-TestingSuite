using System.Text.Json;
using Quality;

string? TryArg(string n) { for (var i = 0; i < args.Length - 1; i++) if (args[i] == n) return args[i + 1]; return null; }
string Arg(string n, string? d = null) => TryArg(n) ?? d ?? throw new ArgumentException("missing arg " + n);

// --tree <workspaceRoot> drives everything (app-project derived). Or --app-project explicitly (e.g. reference).
var tree = TryArg("--tree");
var appProject = TryArg("--app-project") ?? (tree != null ? FindApp(tree) : null)
    ?? throw new ArgumentException("need --tree or --app-project");
var staticRoot = tree ?? Path.GetDirectoryName(Path.GetFullPath(appProject))!;
var mockProject = Arg("--mock-project", "benchmark/mock/MaxioMock.csproj");
var appUrl = Arg("--app-url", "http://127.0.0.1:5121");
var mockUrl = Arg("--mock-url", "http://localhost:8085");
var mode = Arg("--mode", "all");          // deep | drift | metrics | security | dynamic | static | all
var label = Arg("--label", "unlabeled");
var outPath = TryArg("--out");

bool wantDeep = mode is "deep" or "dynamic" or "all";
bool wantDrift = mode is "drift" or "dynamic" or "all";
bool wantMetrics = mode is "metrics" or "static" or "all";
bool wantSecurity = mode is "security" or "static" or "all";

Console.WriteLine($"quality: label={label} mode={mode}");
Console.WriteLine($"  app={appProject}");

D1Report? d1 = null; D2Report? d2 = null; D3Report? d3 = null; D4Report? d4 = null;

// ---- D3/D4 static (no boot) ----
if (wantMetrics) { Console.WriteLine("• D3 static metrics …"); d3 = Metrics.Analyze(staticRoot); }
if (wantSecurity) { Console.WriteLine("• D4 security scan …"); d4 = Security.Analyze(staticRoot); }

// ---- D1/D2 dynamic (boot app + mock) ----
if (wantDeep || wantDrift)
{
    await using var h = new Harness(appProject, mockProject, appUrl, mockUrl);
    Console.WriteLine("• building + booting app + mock …");
    var fail = await h.Start();
    if (fail != null) { Console.WriteLine("[FAIL] " + fail); return 2; }
    if (wantDeep) d1 = await Runner.RunDeep(h);
    if (wantDrift) d2 = await Runner.RunDrift(h);
}

// ---- human-readable summary ----
if (d1 != null)
{
    Console.WriteLine($"\n── D1 correctness depth: {d1.Pass}/{d1.Total} ({d1.Rate:P0}) ──");
    foreach (var c in d1.Checks) Console.WriteLine((c.Pass ? "  [ ok ] " : "  [MISS] ") + c.Id + (c.Pass ? "" : "  — " + c.Detail));
}
if (d2 != null)
{
    Console.WriteLine($"\n── D2 drift: resilience={d2.Resilience:P0}  safety={d2.Safety:P0}  " +
                      $"(correct={d2.Correct} graceful={d2.Graceful} silent-wrong={d2.SilentWrong} broken={d2.Broken}) ──");
    foreach (var cell in d2.Cells)
    {
        var mark = cell.Class switch { "CORRECT" => "  [ ok ] ", "GRACEFUL" => "  [ ~  ] ", "SILENT-WRONG" => "  [SILENT] ", _ => "  [BROKE] " };
        Console.WriteLine($"{mark}{cell.Op} / {cell.Drift} -> {cell.Class} (status={cell.Status})");
    }
}
if (d3 != null)
    Console.WriteLine($"\n── D3 maintainability: files={d3.Files} ownedLoc={d3.OwnedLoc} methods={d3.Methods} " +
                      $"avgCC={d3.AvgCyclomatic} maxCC={d3.MaxCyclomatic} highCC={d3.HighComplexityMethods} " +
                      $"maxNest={d3.MaxNesting} wireCoupling={d3.WireCouplingCount} ──");
if (d4 != null)
    Console.WriteLine($"\n── D4 security: transitiveDeps={d4.TransitiveDeps} vulnerable={d4.VulnerablePackages} " +
                      $"sourceFindings={d4.SourceFindings} ──" + (d4.DepListError.Length > 0 ? $"  (depList: {d4.DepListError})" : ""));

// ---- machine-readable report ----
var report = new
{
    label, mode,
    d1 = d1 is null ? null : new { d1.Pass, d1.Total, d1.Rate, checks = d1.Checks },
    d2 = d2 is null ? null : new { d2.Resilience, d2.Safety, d2.Correct, d2.Graceful, d2.Broken, d2.SilentWrong, cells = d2.Cells },
    d3, d4,
};
var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
if (outPath != null) { File.WriteAllText(outPath, json); Console.WriteLine($"\n• wrote {outPath}"); }
return 0;

static string? FindApp(string treeRoot) =>
    Directory.EnumerateFiles(treeRoot, "PublicApi.csproj", SearchOption.AllDirectories)
        .FirstOrDefault(p => !p.Contains("/obj/") && !p.Contains("\\obj\\"));
