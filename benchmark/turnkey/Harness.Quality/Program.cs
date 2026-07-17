using System.Text;
using System.Text.Json;
using Harness.Core;
using Harness.Quality;

string? TryArg(string n) { for (var i = 0; i < args.Length - 1; i++) if (args[i] == n) return args[i + 1]; return null; }
string Arg(string n, string? d = null) => TryArg(n) ?? d ?? throw new ArgumentException("missing arg " + n);

var profileDir = Path.GetFullPath(Arg("--profile"));
var profile = Json.Load<Profile>(Path.Combine(profileDir, "profile.json"));
var optable = Json.Load<OpTable>(Path.Combine(profileDir, "optable.json"));
var contractPath = Path.GetFullPath(Path.Combine(profileDir, profile.Mock.Contract));

var tree = Path.GetFullPath(Arg("--tree"));
var appProject = TryArg("--app-project") ?? profile.App.Project;
var mode = Arg("--mode", "all");
var outPath = TryArg("--out");
var appUrl = Arg("--app-url", profile.App.BaseUrl);
var mockUrl = Arg("--mock-url", profile.Mock.BaseUrl);
var mockProject = TryArg("--mock-project")
    ?? Proc.FindUp(AppContext.BaseDirectory, Path.Combine("Harness.Mock", "Harness.Mock.csproj"))
    ?? throw new ArgumentException("cannot locate Harness.Mock.csproj (pass --mock-project)");

bool wantDynamic = mode is "deep" or "drift" or "dynamic" or "all";
bool wantStatic = mode is "metrics" or "security" or "static" or "all";
string prefix = profile.App.RoutePrefix;
string[] leakInternals = profile.Leak.Internals;

var scorecard = new Dictionary<string, object?> { ["profile"] = profile.Name, ["tree"] = tree, ["mode"] = mode };

// ---- D1 / D2 (dynamic): boot app + drift-capable mock ------------------------------------------
if (wantDynamic)
{
    if (string.IsNullOrWhiteSpace(appProject)) throw new ArgumentException("dynamic modes need --app-project");
    System.Diagnostics.Process? mock = null, app = null;
    var appLog = new StringBuilder();
    try
    {
        Console.Error.WriteLine("• preflight: " + Proc.Preflight(appProject));
        Console.Error.WriteLine("• building + booting app + mock …");
        if (!Proc.Build(appProject, out var berr)) throw new InvalidOperationException("app build failed:\n" + berr);
        Proc.Build(mockProject, out _);

        mock = Proc.SpawnRun(mockProject, Array.Empty<string>(), new[] { "--contract", contractPath },
            new Dictionary<string, string?> { ["MOCK_URL"] = mockUrl }, null);
        if (!await Proc.WaitHttp(mockUrl + "/__mock/health", () => mock!.HasExited, 60)) throw new InvalidOperationException("mock did not start");

        var env = Proc.BuildEnv(profile, appUrl, mockUrl);
        app = Proc.SpawnRun(appProject, profile.App.LaunchArgs, Array.Empty<string>(), env, appLog);
        if (!await Proc.WaitHttp(appUrl + profile.App.ReadyPath, () => app!.HasExited, 90)) throw new InvalidOperationException("app did not start");

        var appClient = new AppClient(appUrl, profile.App.Headers);
        var mockClient = new MockClient(mockUrl);
        var scope = await Runner.DetectScope(appClient, prefix, optable);
        scorecard["scope"] = scope;
        Console.Error.WriteLine($"• detected scope = {scope}");

        if (mode is "deep" or "dynamic" or "all")
        {
            var d1 = await Runner.RunDeep(appClient, mockClient, prefix, optable, leakInternals, scope);
            scorecard["d1"] = d1;
            Console.Error.WriteLine($"  D1 correctness: {d1.Pass}/{d1.Total} = {d1.Rate:P0}");
        }
        if (mode is "drift" or "dynamic" or "all")
        {
            var d2 = await Runner.RunDrift(appClient, mockClient, prefix, optable, leakInternals, scope);
            scorecard["d2"] = d2;
            Console.Error.WriteLine($"  D2 drift: resilience {d2.Resilience:P0} · safety {d2.Safety:P0} · C{d2.Correct}/G{d2.Graceful}/B{d2.Broken}/SW{d2.SilentWrong}");
        }
    }
    finally { Proc.Kill(app); Proc.Kill(mock); }
}

// ---- D3 / D4 (static): analyze the integration files -------------------------------------------
if (wantStatic)
{
    if (mode is "metrics" or "static" or "all")
    {
        var d3 = Metrics.Analyze(tree, profile.Analysis.IntegrationPathPattern);
        scorecard["d3"] = d3;
        Console.Error.WriteLine($"  D3 maintainability: wire-coupling {d3.WireCouplingCount} · avgCC {d3.AvgCyclomatic} · maxNest {d3.MaxNesting} · LOC {d3.OwnedLoc} · files {d3.Files}");
    }
    if (mode is "security" or "static" or "all")
    {
        var d4 = Security.Analyze(tree, profile.Analysis.IntegrationPathPattern, profile.Analysis.DepProjectFile);
        scorecard["d4"] = d4;
        Console.Error.WriteLine($"  D4 security: source findings {d4.SourceFindings} · transitive deps {d4.TransitiveDeps} · vuln pkgs {d4.VulnerablePackages}");
    }
}

var jsonOut = JsonSerializer.Serialize(scorecard, new JsonSerializerOptions(Json.Options) { WriteIndented = true });
if (outPath is not null)
{
    var outDir = Path.GetDirectoryName(outPath);
    if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
    File.WriteAllText(outPath, jsonOut);
    Console.Error.WriteLine($"• wrote {outPath}");
}
else Console.WriteLine(jsonOut);
return 0;
