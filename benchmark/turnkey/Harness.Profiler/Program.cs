using System.Text;
using Harness.Profiler;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

string? Arg(string n) { for (var i = 0; i < args.Length - 1; i++) if (args[i] == n) return args[i + 1]; return null; }

// diagnostics: load an already-bundled single-file JSON spec with Microsoft.OpenApi and report coverage
if (Arg("--oas-probe") is { } bundledPath) { await OasProbe.Run(bundledPath); return 0; }

var specPath = Arg("--spec") ?? throw new ArgumentException("usage: --spec <openapi.(yaml|json)> --out <profileDir> [--name <name>] [--bundle <out.json>]");
var spec = new Spec(specPath);

// bundle the (possibly multi-file) spec into a single self-contained JSON — external $refs inlined into
// components and rewritten to local pointers. This is also what lets Microsoft.OpenApi load it (its
// external-ref resolver chokes on multi-file specs; a bundled single file has no external refs).
var bundled = new Bundler(spec).Bundle();

if (Arg("--bundle") is { } bundleOut)   // emit the bundle for inspection/reuse
{
    File.WriteAllText(bundleOut, bundled.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"bundled → {bundleOut}");
    return 0;
}

// parse the bundle with Microsoft.OpenApi (native 3.1 + JSON-Schema semantics), then generate the draft
using var ms = new MemoryStream(Encoding.UTF8.GetBytes(bundled.ToJsonString()));
var read = await OpenApiDocument.LoadAsync(ms, "json", new OpenApiReaderSettings { LoadExternalRefs = false });
if (read.Document is null) throw new InvalidOperationException("Microsoft.OpenApi failed to parse the bundled spec");

var outDir = Arg("--out") ?? throw new ArgumentException("missing --out <profileDir>");
var name = Arg("--name") ?? Path.GetFileName(Path.TrimEndingDirectorySeparator(outDir));
return Generator.Run(read.Document, outDir, name);
