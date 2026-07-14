using System.Text.Json;
using System.Text.Json.Nodes;

namespace MaxioMock;

/// <summary>
/// A schema-DRIFT rule the quality harness installs (via POST /__mock/config, field "drift") before a
/// D2 drift-resilience check. Unlike a <see cref="FaultRule"/> (which short-circuits with an error),
/// a drift rule lets the matched route run normally and then MUTATES the outgoing success JSON to
/// simulate realistic upstream API evolution — a renamed/re-typed/removed field, a new enum value,
/// an added field, a scalar that became a union, or a renamed envelope key. The arm's code is never
/// touched: we replay an already-produced integration against a drifted provider and observe whether
/// it still returns correct data, degrades gracefully, breaks, or silently corrupts.
///
/// Installed rules apply while present and are cleared by POST /__mock/reset. With no rules installed
/// the middleware is a no-op, so the mock behaves byte-identically for the Stage-1 token gate.
/// </summary>
public sealed record DriftRule(string? PathContains, string? Method, string Profile, string? Field, string? To);

public sealed class DriftEngine
{
    private readonly object _gate = new();
    private List<DriftRule> _rules = new();

    public void SetRules(IEnumerable<DriftRule>? rules) { lock (_gate) { _rules = rules?.ToList() ?? new(); } }
    public void Reset() { lock (_gate) { _rules.Clear(); } }
    public bool Any { get { lock (_gate) { return _rules.Count > 0; } } }

    /// <summary>The first drift rule matching this request (by method and/or path fragment), or null.</summary>
    public DriftRule? Match(string method, string path)
    {
        lock (_gate)
        {
            foreach (var r in _rules)
            {
                if (r.Method is not null && !r.Method.Equals(method, StringComparison.OrdinalIgnoreCase)) continue;
                if (r.PathContains is not null && !path.Contains(r.PathContains, StringComparison.OrdinalIgnoreCase)) continue;
                return r;
            }
            return null;
        }
    }

    /// <summary>Transform a JSON response body per the drift rule. Returns the original text on parse failure.</summary>
    public static string Apply(DriftRule rule, string json)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(json); } catch { return json; }
        if (root is null) return json;

        var field = rule.Field ?? "";
        switch (rule.Profile.ToLowerInvariant())
        {
            case "additive":                                   // P1: add unknown scalar + nested object
                WalkObjects(root, o => { o["__drift_extra"] = "x"; o["__drift_obj"] = new JsonObject { ["k"] = 1 }; });
                break;
            case "rename":                                     // P2: rename a leaf the arm reads
            case "envelope":                                   // P6: rename the wrapper key (same op)
                WalkObjects(root, o => RenameKey(o, field, rule.To ?? field + "_v2"));
                break;
            case "retype":                                     // P3: number -> string
                WalkObjects(root, o => { if (o.ContainsKey(field) && o[field]?.GetValueKind() == JsonValueKind.Number) o[field] = o[field]!.ToJsonString(); });
                break;
            case "union":                                      // P4: scalar -> { "value": scalar }
                WalkObjects(root, o => { if (o.ContainsKey(field) && IsScalar(o[field])) o[field] = new JsonObject { ["value"] = o[field]?.DeepClone() }; });
                break;
            case "newenum":                                    // P5: emit an unmodeled enum value
                WalkObjects(root, o => { if (o.ContainsKey(field)) o[field] = rule.To ?? "drift_unknown_value"; });
                break;
            case "remove":                                     // P8: drop a field
                WalkObjects(root, o => o.Remove(field));
                break;
        }
        return root.ToJsonString();
    }

    // Visit every JsonObject in the tree. Snapshot children before recursing so mutations don't disturb iteration.
    private static void WalkObjects(JsonNode? node, Action<JsonObject> fn)
    {
        switch (node)
        {
            case JsonObject o:
                var children = o.Select(kv => kv.Value).ToList();
                fn(o);
                foreach (var child in children) WalkObjects(child, fn);
                break;
            case JsonArray a:
                foreach (var item in a.ToList()) WalkObjects(item, fn);
                break;
        }
    }

    private static void RenameKey(JsonObject o, string from, string to)
    {
        if (!o.ContainsKey(from)) return;
        var v = o[from]?.DeepClone();      // detach via clone to avoid parent-conflict
        o.Remove(from);
        o[to] = v;
    }

    private static bool IsScalar(JsonNode? n)
    {
        var k = n?.GetValueKind();
        return k is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False;
    }
}
