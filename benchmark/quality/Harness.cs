using System.Diagnostics;
using System.Text;

namespace Quality;

/// <summary>
/// Boots the app-under-test + the (drift-capable) mock and hands back clients, then tears them down.
/// Process management mirrors the Stage-1 gate (Build / Start / WaitHttp) so quality runs reproduce the
/// exact runtime the token gate verified. The app boots with --no-launch-profile (eShop PublicApi ships a
/// launchSettings.json that would otherwise override the port) and the same Maxio__* config the gate injects.
/// </summary>
public sealed class Harness(string appProject, string mockProject, string appUrl, string mockUrl) : IAsyncDisposable
{
    private Process? _mock, _app;
    private readonly StringBuilder _appLog = new();

    public AppClient App { get; private set; } = null!;
    public MockClient Mock { get; private set; } = null!;
    public string AppLog { get { lock (_appLog) return _appLog.ToString(); } }

    private Dictionary<string, string?> Cfg() => new()
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

    /// <summary>Build + boot both processes. Returns null on success, or a failure reason.</summary>
    public async Task<string?> Start()
    {
        if (!Build(appProject, out var berr)) return "BUILD — app did not compile:\n" + Trunc(berr, 1500);
        Build(mockProject, out _);

        _mock = Spawn($"run --project \"{mockProject}\" --no-build", new() { ["MOCK_URL"] = mockUrl }, null);
        if (!await WaitHttp(mockUrl + "/__mock/health", () => _mock!.HasExited, 60)) return "mock did not start";

        var env = Cfg(); env["ASPNETCORE_URLS"] = appUrl;
        _app = Spawn($"run --project \"{appProject}\" --no-build --no-launch-profile", env, _appLog);
        if (!await WaitHttp(appUrl + "/", () => _app!.HasExited, 90)) return "BOOT — app did not start / crashed at startup";

        App = new AppClient(appUrl);
        Mock = new MockClient(mockUrl);
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        try { _app?.Kill(true); } catch { }
        try { _mock?.Kill(true); } catch { }
        await Task.CompletedTask;
    }

    private static bool Build(string project, out string output)
    {
        var psi = new ProcessStartInfo("dotnet", $"build \"{project}\" -v quiet -clp:ErrorsOnly")
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        var p = Process.Start(psi)!;
        output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode == 0;
    }

    private static Process Spawn(string arguments, Dictionary<string, string?> env, StringBuilder? log)
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

    private static async Task<bool> WaitHttp(string url, Func<bool> exited, int seconds)
    {
        using var h = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        for (var i = 0; i < seconds * 4; i++)
        {
            if (exited()) return false;
            try { await h.GetAsync(url); return true; } catch { }
            await Task.Delay(250);
        }
        return false;
    }

    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..n];
}
