using System.Diagnostics;
using System.Text;

namespace Harness.Core;

/// <summary>
/// Shared process lifecycle for booting the app-under-test + the generic mock (identical mechanics for
/// the gate and the quality tool, so a quality run reproduces the exact runtime the gate verified).
/// Runs the built DLLs directly (single process each → clean teardown, no `dotnet run` child orphans).
/// </summary>
public static class Proc
{
    public static bool Build(string project, out string output)
    {
        var psi = new ProcessStartInfo("dotnet", $"build \"{project}\" -v quiet -clp:ErrorsOnly")
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        var p = Process.Start(psi)!;
        output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode == 0;
    }

    /// <summary>
    /// Spawn a project via `dotnet run --project &lt;proj&gt; --no-build &lt;runArgs&gt; [-- &lt;appArgs&gt;]`, capturing
    /// stdout+stderr into <paramref name="log"/>. Kill(entireProcessTree) reaps the app child on teardown.
    /// </summary>
    public static Process SpawnRun(string project, IEnumerable<string> runArgs, IEnumerable<string> appArgs,
        IDictionary<string, string?> env, StringBuilder? log)
    {
        var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add("run"); psi.ArgumentList.Add("--project"); psi.ArgumentList.Add(project);
        psi.ArgumentList.Add("--no-build");
        foreach (var a in runArgs) psi.ArgumentList.Add(a);
        var app = appArgs.ToList();
        if (app.Count > 0) { psi.ArgumentList.Add("--"); foreach (var a in app) psi.ArgumentList.Add(a); }
        foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;
        var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data != null && log != null) lock (log) log.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null && log != null) lock (log) log.AppendLine(e.Data); };
        p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
        return p;
    }

    /// <summary>Find a file by name walking up from a starting directory (locate Harness.Mock.csproj, etc.).</summary>
    public static string? FindUp(string startDir, string relative)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    public static async Task<bool> WaitHttp(string url, Func<bool> exited, int seconds)
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

    public static void Kill(Process? p) { try { p?.Kill(true); } catch { } }

    /// <summary>Resolve a profile's config dict into concrete env vars (${appUrl}/${mockUrl} substituted),
    /// optionally removing keys (S3) and adding ASPNETCORE_URLS + a BREAK passthrough.</summary>
    public static Dictionary<string, string?> BuildEnv(Profile p, string appUrl, string mockUrl,
        IEnumerable<string>? remove = null)
    {
        var env = new Dictionary<string, string?>();
        foreach (var (k, v) in p.App.Config) env[k] = Json.Resolve(v, appUrl, mockUrl);
        if (remove is not null) foreach (var k in remove) env.Remove(k);
        env["ASPNETCORE_URLS"] = appUrl;
        var brk = Environment.GetEnvironmentVariable("BREAK");
        if (!string.IsNullOrEmpty(brk)) env["BREAK"] = brk;
        return env;
    }
}
