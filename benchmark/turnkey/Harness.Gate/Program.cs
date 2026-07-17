using System.Text;
using Harness.Core;
using Harness.Gate;

string? TryArg(string n) { for (var i = 0; i < args.Length - 1; i++) if (args[i] == n) return args[i + 1]; return null; }
string Arg(string n, string? d = null) => TryArg(n) ?? d ?? throw new ArgumentException("missing arg " + n);

var profileDir = Path.GetFullPath(Arg("--profile"));
var profile = Json.Load<Profile>(Path.Combine(profileDir, "profile.json"));
var optable = Json.Load<OpTable>(Path.Combine(profileDir, "optable.json"));
var contractPath = Path.GetFullPath(Path.Combine(profileDir, profile.Mock.Contract));

var appProject = TryArg("--app-project") ?? profile.App.Project;
if (string.IsNullOrWhiteSpace(appProject)) throw new ArgumentException("no app project (pass --app-project or set profile.app.project)");
var mode = Arg("--mode", "public");
var appUrl = Arg("--app-url", profile.App.BaseUrl);
var mockUrl = Arg("--mock-url", profile.Mock.BaseUrl);
var mockProject = TryArg("--mock-project")
    ?? Proc.FindUp(AppContext.BaseDirectory, Path.Combine("Harness.Mock", "Harness.Mock.csproj"))
    ?? throw new ArgumentException("cannot locate Harness.Mock.csproj (pass --mock-project)");

System.Diagnostics.Process? mock = null, app = null;
var appLog = new StringBuilder();
try
{
    Console.WriteLine($"gate: profile={profile.Name} mode={mode} app={appProject}");
    Console.WriteLine("• preflight: " + Proc.Preflight(appProject));
    Console.WriteLine("• building app + mock …");
    if (!Proc.Build(appProject, out var berr)) { Console.WriteLine("[FAIL] BUILD — app did not compile:\n" + Trunc(berr, 1500)); return 1; }
    Proc.Build(mockProject, out _);

    Console.WriteLine("• starting mock …");
    mock = Proc.SpawnRun(mockProject, Array.Empty<string>(), new[] { "--contract", contractPath },
        new Dictionary<string, string?> { ["MOCK_URL"] = mockUrl }, null);
    if (!await Proc.WaitHttp(mockUrl + "/__mock/health", () => mock!.HasExited, 60)) { Console.WriteLine("[FAIL] mock did not start"); return 1; }

    Console.WriteLine("• starting app …");
    var env = Proc.BuildEnv(profile, appUrl, mockUrl);
    app = Proc.SpawnRun(appProject, profile.App.LaunchArgs, Array.Empty<string>(), env, appLog);
    if (!await Proc.WaitHttp(appUrl + profile.App.ReadyPath, () => app!.HasExited, 90)) { Console.WriteLine("[FAIL] BOOT — app did not start / crashed at startup"); return 1; }

    var ctx = new GateContext(new AppClient(appUrl, profile.App.Headers), new MockClient(mockUrl), () => { lock (appLog) { return appLog.ToString(); } }, profile, optable);
    var checks = mode == "holdout" ? Checks.Holdout(ctx) : Checks.Public(ctx);

    int pass = 0, total = 0;
    foreach (var chk in checks)
    {
        total++;
        CheckResult res;
        try { res = await chk.Run(); } catch (Exception ex) { res = new CheckResult(false, "EXCEPTION: " + ex.Message); }
        Console.WriteLine(res.Passed ? $"  [PASS] {chk.Id}" : $"  [FAIL] {chk.Id} — {res.Detail}");
        if (res.Passed) pass++;
    }

    if (mode == "public")   // S3 needs its own boot with a required secret removed
    {
        var s3 = await S3Check();
        if (s3 is null) Console.WriteLine("  [SKIP] S3.config-fail-fast — no secretConfigKeys declared in profile");
        else { total++; if (s3.Value) { pass++; Console.WriteLine("  [PASS] S3.config-fail-fast"); } else Console.WriteLine("  [FAIL] S3.config-fail-fast — app booted despite a required secret removed"); }
    }

    Console.WriteLine($"\n{pass}/{total} checks passed  ({mode})");
    return pass == total ? 0 : 1;
}
finally
{
    Proc.Kill(app);
    Proc.Kill(mock);
}

// boot a throwaway instance with the required secret(s) removed; production-ready = it must NOT serve.
async Task<bool?> S3Check()
{
    if (profile.App.SecretConfigKeys.Length == 0) return null;
    var u = new Uri(appUrl);
    var url2 = $"{u.Scheme}://{u.Host}:{u.Port + 1}";
    var env = Proc.BuildEnv(profile, url2, mockUrl, remove: profile.App.SecretConfigKeys);
    System.Diagnostics.Process? p = null;
    try
    {
        p = Proc.SpawnRun(appProject, profile.App.LaunchArgs, Array.Empty<string>(), env, null);
        return !await Proc.WaitHttp(url2 + profile.App.ReadyPath, () => p!.HasExited, 25);
    }
    finally { Proc.Kill(p); }
}

static string Trunc(string s, int n) => s.Length <= n ? s : s[..n];
