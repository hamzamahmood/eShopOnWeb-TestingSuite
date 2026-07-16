using Harness.Profiler;

string? Arg(string n) { for (var i = 0; i < args.Length - 1; i++) if (args[i] == n) return args[i + 1]; return null; }
var specPath = Arg("--spec") ?? throw new ArgumentException("usage: --spec <openapi.yaml> --out <profileDir> [--name <name>] [--probe]");
var spec = new Spec(specPath);

if (args.Contains("--probe"))
{
    // diagnostics: report 2xx-example coverage across the spec
    var methods = new[] { "get", "post", "put", "delete", "patch" };
    int ops = 0, withEx = 0;
    foreach (var (_, item) in Spec.Entries(Spec.Get(spec.Root, "paths")))
        foreach (var (m, opNode) in Spec.Entries(item))
        {
            if (!methods.Contains(m)) continue;
            ops++;
            if (Generator.ExtractSuccessExample(spec, opNode).body is not null) withEx++;
        }
    Console.WriteLine($"operations={ops}  with-2xx-example={withEx} ({100.0 * withEx / Math.Max(1, ops):F0}%)");
    return 0;
}

var outDir = Arg("--out") ?? throw new ArgumentException("missing --out <profileDir>");
var name = Arg("--name") ?? Path.GetFileName(Path.TrimEndingDirectorySeparator(outDir));
return Generator.Run(spec, outDir, name);
