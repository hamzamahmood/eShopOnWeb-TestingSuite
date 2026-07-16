using System.Text.Json.Nodes;
using YamlDotNet.RepresentationModel;

namespace Harness.Profiler;

/// <summary>
/// Bundles a multi-file OpenAPI spec into a single self-contained JSON document: every EXTERNAL $ref is
/// copied into the root `components` (under its category) and rewritten to a LOCAL `#/components/...`
/// pointer. Refs stay refs (not inlined), so recursive/cyclic schemas bundle without exploding. The
/// output has zero external refs, so a strict parser (e.g. Microsoft.OpenApi) can load it without any
/// external-reference resolution. Reuses the YamlDotNet resolver, which handles this spec's relative
/// `./components/...` refs correctly.
/// </summary>
public sealed class Bundler(Spec spec)
{
    // category → (localKey → resolved JSON), e.g. "schemas" → { "Product-Response": {...} }
    private readonly Dictionary<string, JsonObject> _components = new();
    private readonly Dictionary<string, (string cat, string key)> _byTarget = new();  // absFile|frag → placement
    private readonly HashSet<string> _usedKeys = new();

    public JsonObject Bundle()
    {
        var root = Spec.ToJson(spec.Root) as JsonObject ?? new JsonObject();
        RewriteRefs(root, spec.Dir);

        var components = root["components"] as JsonObject ?? (JsonObject)(root["components"] = new JsonObject());
        foreach (var (cat, entries) in _components)
        {
            var bucket = components[cat] as JsonObject ?? (JsonObject)(components[cat] = new JsonObject());
            foreach (var k in entries.Select(kv => kv.Key).ToList()) bucket[k] = entries[k]!.DeepClone();
        }
        return root;
    }

    // Rewrite external $refs within a subtree whose refs are relative to baseDir.
    private void RewriteRefs(JsonNode? node, string baseDir)
    {
        switch (node)
        {
            case JsonObject o when o["$ref"] is JsonValue rv && rv.GetValueKind() == System.Text.Json.JsonValueKind.String:
                var refStr = rv.GetValue<string>();
                if (!refStr.StartsWith('#'))
                {
                    var (cat, key) = Intern(refStr, baseDir);
                    o["$ref"] = $"#/components/{cat}/{key}";
                }
                return;   // a ref node has no other meaningful children to walk
            case JsonObject o:
                foreach (var k in o.Select(kv => kv.Key).ToList()) RewriteRefs(o[k], baseDir);
                break;
            case JsonArray a:
                for (var i = 0; i < a.Count; i++) RewriteRefs(a[i], baseDir);
                break;
        }
    }

    // Register an external target, copy it into components (rewriting its own refs), return placement.
    private (string cat, string key) Intern(string refStr, string baseDir)
    {
        var (node, dir) = spec.Resolve(refStr, baseDir);
        var absKey = AbsKey(refStr, baseDir);
        if (_byTarget.TryGetValue(absKey, out var existing)) return existing;

        var cat = Category(refStr);
        var key = UniqueKey(refStr);
        var placement = (cat, key);
        _byTarget[absKey] = placement;                                  // register BEFORE recursing (breaks cycles)

        var json = Spec.ToJson(node) as JsonObject ?? new JsonObject();
        var bucket = _components.TryGetValue(cat, out var b) ? b : (_components[cat] = new JsonObject());
        bucket[key] = json;                                             // placeholder present for cycle-backrefs
        RewriteRefs(json, dir);                                         // refs inside this file are relative to ITS dir
        return placement;
    }

    private static string Category(string refStr)
    {
        var p = refStr.Replace('\\', '/');
        if (p.Contains("/parameters/")) return "parameters";
        if (p.Contains("/responses/")) return "responses";
        if (p.Contains("/requestBodies/")) return "requestBodies";
        if (p.Contains("/examples/")) return "examples";
        if (p.Contains("/headers/")) return "headers";
        return "schemas";
    }

    private string AbsKey(string refStr, string baseDir)
    {
        var hash = refStr.IndexOf('#');
        var file = hash >= 0 ? refStr[..hash] : refStr;
        var frag = hash >= 0 ? refStr[(hash + 1)..] : "";
        var abs = string.IsNullOrEmpty(file) ? baseDir : Path.GetFullPath(Path.Combine(baseDir, file));
        return abs + "|" + frag;
    }

    private string UniqueKey(string refStr)
    {
        var hash = refStr.IndexOf('#');
        var file = hash >= 0 ? refStr[..hash] : refStr;
        var frag = hash >= 0 ? refStr[(hash + 1)..] : "";
        var baseName = string.IsNullOrEmpty(file)
            ? frag.Split('/').LastOrDefault() ?? "ref"
            : Path.GetFileNameWithoutExtension(file);
        if (!string.IsNullOrEmpty(frag) && !string.IsNullOrEmpty(file))
            baseName += "_" + (frag.Split('/').LastOrDefault() ?? "");
        var clean = new string(baseName.Select(c => char.IsLetterOrDigit(c) || c is '_' or '.' or '-' ? c : '_').ToArray());
        var key = clean;
        for (var i = 2; !_usedKeys.Add(key); i++) key = clean + "_" + i;
        return key;
    }
}
