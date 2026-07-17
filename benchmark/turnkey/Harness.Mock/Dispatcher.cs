using System.Text.Json;
using System.Text.Json.Nodes;
using Harness.Core;

namespace Harness.Mock;

/// <summary>
/// Evaluates a contract route against a request: walks the ordered cases, returns the first whose guard
/// holds, and renders its response (fixture or inline body) with {{path.x}} / {{query.x}} / {{body.a.b}}
/// interpolation. Property names are preserved verbatim (JsonNode round-trip) so wire casing — snake_case,
/// camelCase, whatever the provider uses — survives untouched.
/// </summary>
public static class Dispatcher
{
    public sealed record Rendered(int Status, string ContentType, string Body);

    public static Rendered Resolve(RouteDef route, Contract contract,
        IReadOnlyDictionary<string, string?> pathParams,
        IReadOnlyDictionary<string, string?> query,
        JsonElement? body)
    {
        foreach (var c in route.Cases)
        {
            if (!GuardHolds(c.When, pathParams, query, body)) continue;
            return Render(c, contract, pathParams, query, body);
        }
        return new Rendered(404, "application/json", "{\"errors\":[\"not found\"]}");
    }

    // ---- guards -------------------------------------------------------------------------------------
    static bool GuardHolds(GuardSpec? g,
        IReadOnlyDictionary<string, string?> path,
        IReadOnlyDictionary<string, string?> query,
        JsonElement? body)
    {
        if (g is null) return true;

        if (g.PathIn is not null)
            foreach (var (k, allowed) in g.PathIn)
                if (!(path.TryGetValue(k, out var v) && v is not null && allowed.Contains(v, StringComparer.OrdinalIgnoreCase)))
                    return false;

        if (g.QueryPresent is not null)
            foreach (var k in g.QueryPresent)
                if (!(query.TryGetValue(k, out var v) && !string.IsNullOrEmpty(v))) return false;

        if (g.QueryIn is not null)
            foreach (var (k, allowed) in g.QueryIn)
                if (!(query.TryGetValue(k, out var v) && v is not null && allowed.Contains(v, StringComparer.OrdinalIgnoreCase)))
                    return false;

        if (g.BodyPresent is not null)
            foreach (var p in g.BodyPresent)
                if (!BodyHasValue(body, p)) return false;

        if (g.BodyMissing is not null)         // reject branch: matches when ANY required path is absent/empty
            if (!g.BodyMissing.Any(p => !BodyHasValue(body, p))) return false;

        if (g.BodyValueIn is not null)
            foreach (var (p, allowed) in g.BodyValueIn)
            {
                var v = BodyValue(body, p);
                if (v is null || !allowed.Contains(v, StringComparer.OrdinalIgnoreCase)) return false;
            }

        if (g.BodyValueNotIn is not null)      // reject branch: matches when value is NOT in the allowed set
            foreach (var (p, allowed) in g.BodyValueNotIn)
            {
                var v = BodyValue(body, p);
                if (v is not null && allowed.Contains(v, StringComparer.OrdinalIgnoreCase)) return false;
            }

        return true;
    }

    static bool BodyHasValue(JsonElement? body, string dotPath)
    {
        var e = Navigate(body, dotPath);
        if (e is null) return false;
        return e.Value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.String => e.Value.GetString()!.Length > 0,
            JsonValueKind.Object => e.Value.EnumerateObject().Any(),
            JsonValueKind.Array => e.Value.EnumerateArray().Any(),
            _ => true,
        };
    }

    static string? BodyValue(JsonElement? body, string dotPath)
    {
        var e = Navigate(body, dotPath);
        if (e is null) return null;
        return e.Value.ValueKind switch
        {
            JsonValueKind.String => e.Value.GetString(),
            JsonValueKind.Number => e.Value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    static JsonElement? Navigate(JsonElement? root, string dotPath)
    {
        if (root is null) return null;
        var cur = root.Value;
        foreach (var seg in dotPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (cur.ValueKind != JsonValueKind.Object || !cur.TryGetProperty(seg, out var next)) return null;
            cur = next;
        }
        return cur;
    }

    // ---- render + interpolate ----------------------------------------------------------------------
    static Rendered Render(CaseDef c, Contract contract,
        IReadOnlyDictionary<string, string?> path,
        IReadOnlyDictionary<string, string?> query,
        JsonElement? body)
    {
        JsonNode? node = null;
        if (c.BodyFixture is not null)
        {
            if (!contract.Fixtures.TryGetValue(c.BodyFixture, out var fx))
                return new Rendered(500, "application/json", $"{{\"errors\":[\"unknown fixture {c.BodyFixture}\"]}}");
            node = JsonNode.Parse(fx.GetRawText());
        }
        else if (c.Body is { } b)
        {
            node = JsonNode.Parse(b.GetRawText());
        }

        if (node is null) return new Rendered(c.Status, c.ContentType, "");
        var resolved = InterpolateRoot(node, path, query, body);
        return new Rendered(c.Status, c.ContentType, resolved!.ToJsonString());
    }

    // Mutate containers IN PLACE, replacing only tokened string LEAVES. Reassigning an already-parented
    // JsonNode (as a naive recursive-return would) throws "node already has a parent" on JsonArray.
    static JsonNode? InterpolateRoot(JsonNode? node,
        IReadOnlyDictionary<string, string?> path, IReadOnlyDictionary<string, string?> query, JsonElement? body)
    {
        if (node is JsonValue v && v.GetValueKind() == JsonValueKind.String)
        {
            var s = v.GetValue<string>();
            return s.Contains("{{") ? ReplaceLeaf(s, path, query, body) : v;
        }
        InterpolateInPlace(node, path, query, body);
        return node;
    }

    static void InterpolateInPlace(JsonNode? node,
        IReadOnlyDictionary<string, string?> path, IReadOnlyDictionary<string, string?> query, JsonElement? body)
    {
        if (node is JsonObject o)
        {
            foreach (var key in o.Select(kv => kv.Key).ToList())
            {
                var child = o[key];
                if (child is JsonValue cv && cv.GetValueKind() == JsonValueKind.String)
                {
                    var s = cv.GetValue<string>();
                    if (s.Contains("{{")) o[key] = ReplaceLeaf(s, path, query, body);
                }
                else InterpolateInPlace(child, path, query, body);
            }
        }
        else if (node is JsonArray a)
        {
            for (var i = 0; i < a.Count; i++)
            {
                var child = a[i];
                if (child is JsonValue cv && cv.GetValueKind() == JsonValueKind.String)
                {
                    var s = cv.GetValue<string>();
                    if (s.Contains("{{")) a[i] = ReplaceLeaf(s, path, query, body);
                }
                else InterpolateInPlace(child, path, query, body);
            }
        }
    }

    // A tokened string leaf. One token spanning the whole string ⇒ allow numeric coercion
    // (e.g. "id":"{{path.id}}" → 12345). Otherwise splice replacements into the string.
    static JsonNode? ReplaceLeaf(string s,
        IReadOnlyDictionary<string, string?> path, IReadOnlyDictionary<string, string?> query, JsonElement? body)
    {
        var single = Tokens.SingleMatch(s);
        if (single is not null)
        {
            var raw = ResolveToken(single, path, query, body) ?? "";
            if (long.TryParse(raw, out var l)) return JsonValue.Create(l);
            if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d)) return JsonValue.Create(d);
            return JsonValue.Create(raw);
        }
        return JsonValue.Create(Tokens.Replace(s, t => ResolveToken(t, path, query, body) ?? ""));
    }

    static string? ResolveToken(string token, IReadOnlyDictionary<string, string?> path,
        IReadOnlyDictionary<string, string?> query, JsonElement? body)
    {
        var dot = token.IndexOf('.');
        if (dot < 0) return null;
        var scope = token[..dot];
        var rest = token[(dot + 1)..];
        return scope switch
        {
            "path" => path.GetValueOrDefault(rest),
            "query" => query.GetValueOrDefault(rest),
            "body" => BodyValue(body, rest),
            _ => null,
        };
    }
}

/// <summary>{{ ... }} token scanning shared by the interpolator.</summary>
static class Tokens
{
    static readonly System.Text.RegularExpressions.Regex Re = new(@"\{\{\s*([^}]+?)\s*\}\}");

    public static string? SingleMatch(string s)
    {
        var ms = Re.Matches(s);
        if (ms.Count == 1 && ms[0].Value.Length == s.Length) return ms[0].Groups[1].Value;
        return null;
    }

    public static string Replace(string s, Func<string, string> resolve) =>
        Re.Replace(s, m => resolve(m.Groups[1].Value));
}
