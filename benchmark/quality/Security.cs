using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Quality;

public sealed record D4Report(
    int TransitiveDeps, int VulnerablePackages, IReadOnlyList<string> VulnerableDetail,
    int SourceFindings, IReadOnlyList<string> SourceDetail, string DepListError);

/// <summary>
/// D4 security depth (static, no boot). Two-sided by construction:
///  • Supply-chain surface — transitive dependency count + `dotnet list --vulnerable`. The generated SDK
///    adds a dependency graph the hand-rolled arm does not (a real con for the SDK); the hand-rolled arm
///    owns hand-written auth (a con the source scan can catch). Counted on the Infrastructure project
///    (where the provider client + any SDK package live).
///  • Source scan — regex sweep of the integration files for hardcoded secrets, non-TLS base URLs baked
///    in code, and disabled TLS/cert validation.
/// </summary>
public static class Security
{
    static readonly (Regex re, string label)[] SourceRules =
    {
        (new(@"ApiKey\s*=\s*""[^""]+""", RegexOptions.IgnoreCase), "hardcoded api key literal"),
        (new(@"""(sk|key|secret|pwd|password)_[A-Za-z0-9]{6,}""", RegexOptions.IgnoreCase), "hardcoded secret-shaped literal"),
        (new(@"http://(?!localhost|127\.0\.0\.1)", RegexOptions.IgnoreCase), "non-TLS http:// endpoint in code"),
        (new(@"DangerousAcceptAnyServerCertificateValidator|ServerCertificateCustomValidationCallback\s*=\s*.*true", RegexOptions.IgnoreCase), "TLS/cert validation disabled"),
        (new(@"ServicePointManager\.ServerCertificateValidationCallback", RegexOptions.IgnoreCase), "global cert validation override"),
    };

    public static D4Report Analyze(string treeRoot)
    {
        var infra = FindProject(treeRoot, "Infrastructure.csproj") ?? FindProject(treeRoot, "PublicApi.csproj");

        int deps = 0, vuln = 0; var vulnDetail = new List<string>(); var depErr = "";
        if (infra != null)
        {
            var (transOut, tErr) = Run("dotnet", $"list \"{infra}\" package --include-transitive", 120);
            deps = Regex.Matches(transOut, @"^\s*>\s+\S+", RegexOptions.Multiline).Count;
            if (deps == 0 && tErr.Length > 0) depErr = Trunc(tErr, 200);

            var (vulnOut, _) = Run("dotnet", $"list \"{infra}\" package --vulnerable --include-transitive", 180);
            foreach (Match m in Regex.Matches(vulnOut, @"^\s*>\s+(\S+)\s+.*?(Critical|High|Moderate|Low)", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                vuln++; if (vulnDetail.Count < 15) vulnDetail.Add($"{m.Groups[1].Value} ({m.Groups[2].Value})");
            }
        }

        int findings = 0; var srcDetail = new List<string>();
        foreach (var f in Metrics.IntegrationFiles(treeRoot))
        {
            var text = File.ReadAllText(f);
            foreach (var (re, label) in SourceRules)
                foreach (Match _ in re.Matches(text)) { findings++; if (srcDetail.Count < 12) srcDetail.Add($"{label} @ {Path.GetFileName(f)}"); }
        }

        return new D4Report(deps, vuln, vulnDetail, findings, srcDetail, depErr);
    }

    static string? FindProject(string root, string name) =>
        Directory.EnumerateFiles(root, name, SearchOption.AllDirectories)
            .FirstOrDefault(p => !p.Contains("/obj/") && !p.Contains("\\obj\\"));

    static (string stdout, string stderr) Run(string exe, string args, int seconds)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, args)
                { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            var p = Process.Start(psi)!;
            var o = p.StandardOutput.ReadToEnd(); var e = p.StandardError.ReadToEnd();
            if (!p.WaitForExit(seconds * 1000)) { try { p.Kill(true); } catch { } return (o, "timeout"); }
            return (o, e);
        }
        catch (Exception ex) { return ("", ex.Message); }
    }

    static string Trunc(string s, int n) => s.Length <= n ? s : s[..n];
}
