using System.Text.Json;
using Quality;

string? TryArg(string n) { for (var i = 0; i < args.Length - 1; i++) if (args[i] == n) return args[i + 1]; return null; }
string Arg(string n, string? d = null) => TryArg(n) ?? d ?? throw new ArgumentException("missing arg " + n);

var appProject = Arg("--app-project");
var mockProject = Arg("--mock-project", "benchmark/mock/MaxioMock.csproj");
var appUrl = Arg("--app-url", "http://127.0.0.1:5121");
var mockUrl = Arg("--mock-url", "http://localhost:8085");
var mode = Arg("--mode", "both");         // deep | drift | both
var label = Arg("--label", "unlabeled");
var outPath = TryArg("--out");

Console.WriteLine($"quality: label={label} mode={mode}");
Console.WriteLine($"  app={appProject}");

await using var h = new Harness(appProject, mockProject, appUrl, mockUrl);
Console.WriteLine("• building + booting app + mock …");
var fail = await h.Start();
if (fail != null) { Console.WriteLine("[FAIL] " + fail); return 2; }

D1Report? d1 = null; D2Report? d2 = null;
if (mode is "deep" or "both") d1 = await Runner.RunDeep(h);
if (mode is "drift" or "both") d2 = await Runner.RunDrift(h);

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
        Console.WriteLine($"{mark}{cell.Op} / {cell.Drift} → {cell.Class} (status={cell.Status})");
    }
}

// ---- machine-readable report ----
var report = new
{
    label, mode,
    d1 = d1 is null ? null : new { d1.Pass, d1.Total, d1.Rate, checks = d1.Checks },
    d2 = d2 is null ? null : new { d2.Resilience, d2.Safety, d2.Correct, d2.Graceful, d2.Broken, d2.SilentWrong, cells = d2.Cells },
};
var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
if (outPath != null) { File.WriteAllText(outPath, json); Console.WriteLine($"\n• wrote {outPath}"); }

return 0;
