using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace Harness.Profiler;

/// <summary>Probe: does Microsoft.OpenApi v3 load a self-contained (bundled) single-file JSON spec
/// cleanly — i.e., does pre-bundling sidestep the external-$ref UriFormatException?</summary>
public static class OasProbe
{
    public static async Task Run(string bundledJsonPath)
    {
        var settings = new OpenApiReaderSettings { LoadExternalRefs = false };
        var result = await OpenApiDocument.LoadAsync(bundledJsonPath, settings: settings);
        var doc = result.Document!;
        Console.WriteLine($"LOADED OK  paths={doc.Paths.Count}  errors={result.Diagnostic?.Errors.Count}");

        int ops = 0, withExample = 0, schemaReadable = 0;
        foreach (var (_, item) in doc.Paths)
            foreach (var op in item.Operations ?? new Dictionary<HttpMethod, OpenApiOperation>())
            {
                ops++;
                foreach (var (code, resp) in op.Value.Responses ?? new())
                {
                    if (!code.StartsWith('2')) continue;
                    var mt = resp.Content?.Values.FirstOrDefault();
                    if (mt is null) continue;
                    if (mt.Example is not null || (mt.Examples?.Count ?? 0) > 0) withExample++;
                    if (mt.Schema is { } s && (s.Properties?.Count > 0 || s.Type is not null || s.Items is not null)) schemaReadable++;
                    break;
                }
            }
        Console.WriteLine($"operations={ops}  with-example={withExample}  schema-readable={schemaReadable}");

        // show that we can resolve a bundled $ref schema (the whole point)
        foreach (var (pathKey, item) in doc.Paths)
        {
            if (!pathKey.Contains("products.json")) continue;
            foreach (var op in item.Operations!)
                foreach (var (code, resp) in op.Value.Responses!)
                {
                    if (!code.StartsWith('2')) continue;
                    var s = resp.Content?.Values.FirstOrDefault()?.Schema;
                    Console.WriteLine($"  {op.Key} {pathKey} {code}: schemaType={s?.Type} props={s?.Properties?.Count}");
                    break;
                }
            break;
        }
    }
}
