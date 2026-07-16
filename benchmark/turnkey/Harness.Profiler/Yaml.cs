using System.Globalization;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Harness.Profiler;

/// <summary>
/// Minimal OpenAPI loader over YamlDotNet's representation model — enough to walk paths/operations,
/// pull inline response examples (example-first), and resolve $refs (local + external file) for the
/// schema-synthesis fallback. Uses the representation model (not object deserialization) so scalar
/// TYPES survive: an unquoted `id: 4364984` becomes a JSON number, a quoted `"123"` stays a string.
/// </summary>
public sealed class Spec
{
    public YamlMappingNode Root { get; }
    public string Dir { get; }
    private readonly Dictionary<string, YamlNode> _cache = new();

    public Spec(string path)
    {
        var full = Path.GetFullPath(path);
        Dir = Path.GetDirectoryName(full)!;
        Root = (YamlMappingNode)Load(full);
    }

    private YamlNode Load(string full)
    {
        if (_cache.TryGetValue(full, out var n)) return n;
        using var r = new StreamReader(full);
        var ys = new YamlStream();
        ys.Load(r);
        var root = ys.Documents[0].RootNode;
        _cache[full] = root;
        return root;
    }

    /// <summary>Child of a mapping by key (iterates — avoids relying on YamlScalarNode key equality).</summary>
    public static YamlNode? Get(YamlNode? n, string key)
    {
        if (n is YamlMappingNode m)
            foreach (var kv in m.Children)
                if (kv.Key is YamlScalarNode k && k.Value == key) return kv.Value;
        return null;
    }

    public static IEnumerable<(string key, YamlNode val)> Entries(YamlNode? n)
    {
        if (n is YamlMappingNode m)
            foreach (var kv in m.Children)
                if (kv.Key is YamlScalarNode k) yield return (k.Value!, kv.Value);
    }

    public static string? Scalar(YamlNode? n) => n is YamlScalarNode s ? s.Value : null;

    /// <summary>Follow a $ref chain (local #/… or external file[#/…]) to the concrete node, tracking the
    /// base dir so nested external refs resolve relative to their own file.</summary>
    public (YamlNode node, string baseDir) Deref(YamlNode node, string baseDir)
    {
        var seen = new HashSet<string>();
        while (Get(node, "$ref") is YamlScalarNode { Value: { } refStr })
        {
            if (!seen.Add(baseDir + "|" + refStr)) break;
            (node, baseDir) = ResolveRef(refStr, baseDir);
        }
        return (node, baseDir);
    }

    /// <summary>Public wrapper: load the node a $ref string points at, plus the base dir of the file it
    /// landed in (so refs inside that node resolve relative to their own file). Used by the bundler.</summary>
    public (YamlNode node, string dir) Resolve(string refStr, string baseDir) => ResolveRef(refStr, baseDir);

    private (YamlNode, string) ResolveRef(string refStr, string baseDir)
    {
        var hash = refStr.IndexOf('#');
        var file = hash >= 0 ? refStr[..hash] : refStr;
        var frag = hash >= 0 ? refStr[(hash + 1)..] : "";
        YamlNode docRoot; string newBase;
        if (string.IsNullOrEmpty(file)) { docRoot = Root; newBase = Dir; }
        else
        {
            var full = Path.GetFullPath(Path.Combine(baseDir, file));
            newBase = Path.GetDirectoryName(full)!;
            docRoot = Load(full);
        }
        var node = string.IsNullOrEmpty(frag) ? docRoot : Navigate(docRoot, frag);
        return (node, newBase);
    }

    private static YamlNode Navigate(YamlNode root, string frag)
    {
        var cur = root;
        foreach (var raw in frag.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var seg = raw.Replace("~1", "/").Replace("~0", "~");
            var next = Get(cur, seg);
            if (next is null && cur is YamlSequenceNode s && int.TryParse(seg, out var i) && i < s.Children.Count) next = s.Children[i];
            cur = next ?? throw new InvalidOperationException($"$ref fragment segment '{seg}' not found");
        }
        return cur;
    }

    /// <summary>Convert an inline YAML value (e.g. an example) to a JsonNode, preserving scalar types.</summary>
    public static JsonNode? ToJson(YamlNode? node)
    {
        switch (node)
        {
            case YamlMappingNode m:
                var o = new JsonObject();
                foreach (var (k, v) in Entries(m)) o[k] = ToJson(v);
                return o;
            case YamlSequenceNode s:
                var a = new JsonArray();
                foreach (var it in s.Children) a.Add(ToJson(it));
                return a;
            case YamlScalarNode sc:
                return ScalarToJson(sc);
            default:
                return null;
        }
    }

    private static JsonNode? ScalarToJson(YamlScalarNode sc)
    {
        var v = sc.Value ?? "";
        if (sc.Style is ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted) return JsonValue.Create(v);
        if (v is "null" or "~" or "") return null;
        if (v is "true" or "True" or "TRUE") return JsonValue.Create(true);
        if (v is "false" or "False" or "FALSE") return JsonValue.Create(false);
        if (long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return JsonValue.Create(l);
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return JsonValue.Create(d);
        return JsonValue.Create(v);
    }
}
