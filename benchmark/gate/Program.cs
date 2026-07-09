using System.Diagnostics;
using System.Text;
using Gate;

string? TryArg(string n) { for (var i = 0; i < args.Length - 1; i++) if (args[i] == n) return args[i + 1]; return null; }
string Arg(string n, string? d = null) => TryArg(n) ?? d ?? throw new ArgumentException("missing arg " + n);

var appProject = Arg("--app-project");
var mockProject = Arg("--mock-project");
var appUrl = Arg("--app-url", "http://127.0.0.1:5111");
var mockUrl = Arg("--mock-url", "http://localhost:8080");
var mode = Arg("--mode", "public");
var brk = Environment.GetEnvironmentVariable("BREAK");

Dictionary<string, string?> Cfg() => new()
{
    ["Maxio__BaseUrl"] = mockUrl,
    ["Maxio__ApiKey"] = "test-api-key",
    ["Maxio__Subdomain"] = "acme",
    ["Maxio__ProductFamilyId"] = "600001",
    ["Maxio__ProductFamilyHandle"] = "eshop-plans",
    ["Maxio__MeteredComponentId"] = "800001",
    ["Maxio__MeteredComponentHandle"] = "api-calls",
    ["UseOnlyInMemoryDatabase"] = "true",
    ["ASPNETCORE_ENVIRONMENT"] = "Development",
    ["DOTNET_ROLL_FORWARD"] = "Major",
};

Process? mock = null, app = null;
var appLog = new StringBuilder();
try
{
    Console.WriteLine($"gate: mode={mode}  app={appProject}");
    Console.WriteLine("• building app + mock …");
    if (!Build(appProject, out var berr)) { Console.WriteLine("[FAIL] BUILD — app did not compile:\n" + Trunc(berr, 1500)); return 1; }
    Build(mockProject, out _);

    Console.WriteLine("• starting mock …");
    mock = Start($"run --project \"{mockProject}\" --no-build", new() { ["MOCK_URL"] = mockUrl }, null);
    if (!await WaitHttp(mockUrl + "/__mock/health", () => mock!.HasExited, 60)) { Console.WriteLine("[FAIL] mock did not start"); return 1; }

    Console.WriteLine("• starting app …");
    var env = Cfg(); env["ASPNETCORE_URLS"] = appUrl; if (brk != null) env["BREAK"] = brk;
    app = Start($"run --project \"{appProject}\" --no-build --no-launch-profile", env, appLog);
    if (!await WaitHttp(appUrl + "/", () => app!.HasExited, 90)) { Console.WriteLine("[FAIL] BOOT — app did not start / crashed at startup"); return 1; }

    var ctx = new GateContext(new AppClient(appUrl), new MockClient(mockUrl), () => { lock (appLog) { return appLog.ToString(); } });
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

    if (mode == "public")   // S3 needs its own boot, so it lives in the orchestrator
    {
        total++;
        var ok = await S3Check();
        Console.WriteLine(ok ? "  [PASS] S3.config-fail-fast" : "  [FAIL] S3.config-fail-fast — app booted despite missing Maxio__ApiKey");
        if (ok) pass++;
    }

    Console.WriteLine($"\n{pass}/{total} checks passed  ({mode})");
    return pass == total ? 0 : 1;
}
finally
{
    try { app?.Kill(true); } catch { }
    try { mock?.Kill(true); } catch { }
}

// boot a throwaway instance with the required key removed; production-ready = it must NOT serve.
async Task<bool> S3Check()
{
    var u = new Uri(appUrl);
    var url2 = $"{u.Scheme}://{u.Host}:{u.Port + 1}";
    var env = Cfg(); env.Remove("Maxio__ApiKey"); env["ASPNETCORE_URLS"] = url2;
    Process? p = null;
    try { p = Start($"run --project \"{appProject}\" --no-build --no-launch-profile", env, null); return !await WaitHttp(url2 + "/", () => p!.HasExited, 25); }
    finally { try { p?.Kill(true); } catch { } }
}

bool Build(string project, out string output)
{
    var psi = new ProcessStartInfo("dotnet", $"build \"{project}\" -v quiet -clp:ErrorsOnly")
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    var p = Process.Start(psi)!;
    output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
    p.WaitForExit();
    return p.ExitCode == 0;
}

Process Start(string arguments, Dictionary<string, string?> env, StringBuilder? log)
{
    var psi = new ProcessStartInfo("dotnet", arguments)
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;
    var p = new Process { StartInfo = psi };
    p.OutputDataReceived += (_, e) => { if (e.Data != null && log != null) lock (log) log.AppendLine(e.Data); };
    p.ErrorDataReceived += (_, e) => { if (e.Data != null && log != null) lock (log) log.AppendLine(e.Data); };
    p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
    return p;
}

async Task<bool> WaitHttp(string url, Func<bool> exited, int seconds)
{
    using var h = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    for (var i = 0; i < seconds * 4; i++)
    {
        if (exited()) return false;
        try { await h.GetAsync(url); return true; } catch { /* not up yet */ }
        await Task.Delay(250);
    }
    return false;
}

string Trunc(string s, int n) => s.Length <= n ? s : s[..n];
