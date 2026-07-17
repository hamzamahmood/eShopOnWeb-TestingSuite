using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Harness.Core;

/// <summary>
/// Shared process lifecycle for booting the app-under-test + the generic mock (identical mechanics for
/// the gate and the quality tool, so a quality run reproduces the exact runtime the gate verified).
/// Runs the built DLLs directly (single process each → clean teardown, no `dotnet run` child orphans).
/// </summary>
public static class Proc
{
    /// <summary>One-line preflight surfacing the SDK/TFM/global.json interaction that silently governs the
    /// build. Prints: the SDK version `dotnet` resolves in the HARNESS cwd (which is where the build runs);
    /// the app's target framework (searched in the csproj, then in an ancestor Directory.Build.props /
    /// Directory.Packages.props); and — the actual trap — whether the app tree carries a global.json
    /// pinning an SDK. The kit builds an older-TFM app under a newer SDK only because it runs `dotnet` from
    /// its own cwd (no ancestor global.json) with roll-forward on; a global.json pinning an uninstalled SDK
    /// would break a build run from the app dir. Making all three visible turns a confusing SDK-resolution
    /// failure into an obvious one. Never throws.</summary>
    public static string Preflight(string appProject)
    {
        string sdk = "unknown";
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--version")
                { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            var p = Process.Start(psi)!;
            sdk = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            if (string.IsNullOrWhiteSpace(sdk)) sdk = "unknown";
        }
        catch { /* dotnet not on PATH — the build step will report it */ }

        var appDir = Path.GetDirectoryName(Path.GetFullPath(appProject)) ?? ".";
        string tfm = FindTfm(appProject) ?? "?";

        string pin = "";
        try
        {
            var gj = FindUp(appDir, "global.json");
            if (gj is not null)
            {
                var m = Regex.Match(File.ReadAllText(gj), "\"version\"\\s*:\\s*\"([^\"]+)\"");
                if (m.Success)
                    pin = $" · app global.json pins SDK {m.Groups[1].Value} (not applied — harness builds from its own cwd)";
            }
        }
        catch { /* global.json unreadable — not fatal here */ }

        return $".NET SDK {sdk} · app TFM {tfm}{pin}";
    }

    /// <summary>The app's target framework: the csproj if it declares one, else the nearest ancestor
    /// Directory.Build.props / Directory.Packages.props that does (some projects pin the TFM centrally via
    /// an MSBuild import rather than per-project).</summary>
    private static string? FindTfm(string appProject)
    {
        static string? FromText(string path)
        {
            try
            {
                var m = Regex.Match(File.ReadAllText(path), @"<TargetFrameworks?>([^<]+)</");
                return m.Success ? m.Groups[1].Value.Trim() : null;
            }
            catch { return null; }
        }

        var fromProj = FromText(appProject);
        if (fromProj is not null) return fromProj;

        var dir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(appProject)) ?? ".");
        while (dir is not null)
        {
            foreach (var name in new[] { "Directory.Build.props", "Directory.Packages.props" })
            {
                var props = Path.Combine(dir.FullName, name);
                if (File.Exists(props) && FromText(props) is { } tfm) return tfm;
            }
            dir = dir.Parent;
        }
        return null;
    }

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
