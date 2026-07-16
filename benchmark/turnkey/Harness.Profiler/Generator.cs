using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.OpenApi;

namespace Harness.Profiler;

/// <summary>
/// Emits a profile DRAFT from a loaded OpenAPI document (Microsoft.OpenApi model, fed the bundled
/// single-file spec). Provider-side fields (mock routes, fixtures, upstream paths, expected id values,
/// drift candidates) are filled from the spec — example-first, with schema synthesis when an operation
/// has no example. Integration-side fields the spec cannot know (app-routes, roles, boot config) are
/// emitted as TODO placeholders for the agent to complete.
/// </summary>
public static class Generator
{
    static readonly string[] IdKeys = { "id", "uid", "code", "handle", "token", "reference" };
    static int _sentinel;   // deterministic distinct sentinel ids for schema-synthesized fixtures

    public sealed record GenOp(
        string Id, string Method, string Path, string SuccessStatus, JsonNode? Example,
        string? IdField, string? IdValue, string UpstreamFragment, string? RequestBody);

    public static int Run(OpenApiDocument doc, string outDir, string name)
    {
        Directory.CreateDirectory(outDir);
        _sentinel = 1000001;

        var gens = new List<GenOp>();
        var noExample = new List<string>();
        foreach (var (pathKey, item) in doc.Paths)
        {
            if (item.Operations is null) continue;
            foreach (var (method, op) in item.Operations)
            {
                var g = Build(pathKey, method.Method, op);
                gens.Add(g);
                if (g.Example is null) noExample.Add($"{method.Method.ToUpperInvariant()} {pathKey}");
            }
        }

        WriteContract(outDir, gens);
        WriteOptableDraft(outDir, gens);
        WriteProfileSkeleton(doc, outDir, name);
        PrintChecklist(gens, noExample, outDir);
        return 0;
    }

    static GenOp Build(string path, string httpMethod, OpenApiOperation op)
    {
        var method = httpMethod.ToLowerInvariant();
        var (status, example) = SuccessExample(op);
        var (idField, idValue) = example is null ? (null, null) : FindPrimaryId(example);
        var opId = !string.IsNullOrEmpty(op.OperationId)
            ? Kebab(op.OperationId!) : $"{method}-{LastLiteral(path).TrimEnd('.', 'j', 's', 'o', 'n')}";
        return new GenOp(opId, method, path, status ?? "200", example, idField, idValue, UpstreamFragment(path), RequestExample(op));
    }

    // ---- Microsoft.OpenApi extraction ---------------------------------------------------------------
    static (string? status, JsonNode? body) SuccessExample(OpenApiOperation op)
    {
        if (op.Responses is null) return (null, null);
        foreach (var (code, resp) in op.Responses)
        {
            if (!(code.StartsWith('2') || code == "default")) continue;
            var mt = MediaType(resp.Content);
            if (mt is null) return (code, null);
            if (mt.Example is not null) return (code, mt.Example.DeepClone());
            var exVal = mt.Examples?.Values.FirstOrDefault()?.Value;
            if (exVal is not null) return (code, exVal.DeepClone());
            if (mt.Schema is not null && Synth(mt.Schema, 0) is { } synth) return (code, synth);
            return (code, null);
        }
        return (null, null);
    }

    static string? RequestExample(OpenApiOperation op)
    {
        var mt = MediaType(op.RequestBody?.Content);
        if (mt is null) return null;
        var v = mt.Example ?? mt.Examples?.Values.FirstOrDefault()?.Value;
        if (v is not null) return v.ToJsonString();
        return mt.Schema is not null ? Synth(mt.Schema, 0)?.ToJsonString() : null;
    }

    static IOpenApiMediaType? MediaType(IDictionary<string, IOpenApiMediaType>? content)
    {
        if (content is null) return null;
        return content.TryGetValue("application/json", out var mt) ? mt : content.Values.FirstOrDefault();
    }

    // ---- schema-based fixture synthesis (MO model; fallback when no inline example) -----------------
    // Objects from properties, arrays from items, enum→first value, id-ish integers→distinct sentinels,
    // typed strings by format. Depth-capped so recursive schema graphs terminate. MO resolves $refs.
    static JsonNode? Synth(IOpenApiSchema schema, int depth)
    {
        if (depth > 6) return null;
        if (schema.Example is not null) return schema.Example.DeepClone();

        if (schema.AllOf is { Count: > 0 } allOf)
        {
            var merged = new JsonObject();
            foreach (var sub in allOf)
                if (Synth(sub, depth) is JsonObject o)
                    foreach (var k in o.Select(kv => kv.Key).ToList()) { var v = o[k]; o.Remove(k); merged[k] = v; }
            MergeProps(schema, merged, depth);
            return merged;
        }
        if (schema.OneOf is { Count: > 0 } oneOf) return Synth(oneOf[0], depth);
        if (schema.AnyOf is { Count: > 0 } anyOf) return Synth(anyOf[0], depth);

        if (Is(schema.Type, JsonSchemaType.Object) || schema.Properties is { Count: > 0 })
        {
            var obj = new JsonObject();
            MergeProps(schema, obj, depth);
            return obj;
        }
        if (Is(schema.Type, JsonSchemaType.Array))
        {
            var arr = new JsonArray();
            if (schema.Items is not null && Synth(schema.Items, depth + 1) is { } iv) arr.Add(iv);
            return arr;
        }
        return ScalarSynth(schema);
    }

    static void MergeProps(IOpenApiSchema s, JsonObject obj, int depth)
    {
        if (s.Properties is null) return;
        foreach (var (name, ps) in s.Properties) obj[name] = Synth(ps, depth + 1);
    }

    static bool Is(JsonSchemaType? t, JsonSchemaType flag) => t.HasValue && (t.Value & flag) == flag;

    static JsonNode? ScalarSynth(IOpenApiSchema s)
    {
        if (s.Enum is { Count: > 0 } en && en[0] is not null) return en[0]!.DeepClone();
        if (Is(s.Type, JsonSchemaType.Integer)) return JsonValue.Create(_sentinel++);
        if (Is(s.Type, JsonSchemaType.Number)) return JsonValue.Create((double)_sentinel++);
        if (Is(s.Type, JsonSchemaType.Boolean)) return JsonValue.Create(false);
        if (Is(s.Type, JsonSchemaType.String))
            return JsonValue.Create(s.Format switch
            {
                "date" => "2024-01-01",
                "date-time" => "2024-01-01T00:00:00Z",
                "email" => "user@example.com",
                "uuid" => "00000000-0000-0000-0000-000000000000",
                "uri" => "https://example.com",
                _ => "string",
            });
        return null;
    }

    static string AuthHint(OpenApiDocument doc)
    {
        var schemes = doc.Components?.SecuritySchemes;
        if (schemes is null || schemes.Count == 0) return "none declared";
        return string.Join(", ", schemes.Select(kv =>
            $"{kv.Key} ({kv.Value.Type}{(string.IsNullOrEmpty(kv.Value.Scheme) ? "" : "/" + kv.Value.Scheme)})"));
    }

    // ---- contract.json ------------------------------------------------------------------------------
    static void WriteContract(string outDir, List<GenOp> gens)
    {
        var routes = new JsonArray();
        foreach (var g in gens)
        {
            if (g.Example is null) continue;   // no synthesizable body; listed in the checklist
            var cases = new JsonArray();
            // A pathIn guard (known-id → 200, else 404) is only correct for a READ-BY-ID: an idempotent
            // method whose TERMINAL path segment is the id param. Creates (POST) and collection lists
            // (parent param, literal tail) must NOT guard on the returned entity's id.
            var lastSeg = g.Path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
            var terminalParam = Regex.Match(lastSeg, @"\{([^}]+)\}");
            var byId = g.Method is "get" or "delete" or "put" && terminalParam.Success && g.IdValue is not null;
            if (byId)
            {
                var param = terminalParam.Groups[1].Value;
                // The guard must key on the value the integration will actually place in THIS path param —
                // which is the example's field matching the param NAME, not the entity's generic primary id.
                // (e.g. /user/{username} keys on "theUser", not the User's id 10.)
                var guardValue = GuardValue(g.Example!, param) ?? g.IdValue;
                cases.Add(new JsonObject
                {
                    ["when"] = new JsonObject { ["pathIn"] = new JsonObject { [param] = new JsonArray(guardValue) } },
                    ["status"] = StatusInt(g.SuccessStatus),
                    ["body"] = g.Example!.DeepClone(),
                });
                cases.Add(new JsonObject { ["status"] = 404 });
            }
            else
            {
                cases.Add(new JsonObject { ["status"] = StatusInt(g.SuccessStatus), ["body"] = g.Example!.DeepClone() });
            }
            routes.Add(new JsonObject { ["method"] = g.Method.ToUpperInvariant(), ["path"] = g.Path, ["cases"] = cases });
        }
        var contract = new JsonObject
        {
            ["//"] = "GENERATED from the OpenAPI spec (example-first, schema-synth fallback). Fixtures carry the " +
                     "provider's real shapes. Add error/guard cases (422 on missing fields, bad-reference rejects) per op as needed.",
            ["fixtures"] = new JsonObject(),
            ["routes"] = routes,
        };
        Write(outDir, "contract.json", contract);
    }

    // ---- optable.draft.json -------------------------------------------------------------------------
    static void WriteOptableDraft(string outDir, List<GenOp> gens)
    {
        var ops = new JsonArray();
        foreach (var g in gens)
        {
            var app = new JsonObject
            {
                ["//"] = "TODO: the integration's app route that triggers this provider call (relative to routePrefix)",
                ["method"] = g.Method.ToUpperInvariant(),
                ["path"] = "TODO",
            };
            if (g.RequestBody is not null) app["body"] = g.RequestBody;

            var op = new JsonObject
            {
                ["id"] = g.Id,
                ["scope"] = 0,
                ["app"] = app,
                ["upstream"] = new JsonObject { ["method"] = g.Method.ToUpperInvariant(), ["pathContains"] = g.UpstreamFragment },
                ["gate"] = new JsonObject { ["mustContain"] = g.IdValue is null ? new JsonArray() : new JsonArray(g.IdValue) },
            };
            if (g.IdField is not null)
                op["drifts"] = new JsonArray
                {
                    new JsonObject { ["label"] = $"rename {g.IdField}", ["profile"] = "rename", ["field"] = g.IdField, ["to"] = g.IdField + "_v2", ["check"] = "Values" },
                    new JsonObject { ["label"] = $"retype {g.IdField}", ["profile"] = "retype", ["field"] = g.IdField, ["check"] = "Values" },
                    new JsonObject { ["label"] = "additive", ["profile"] = "additive", ["check"] = "Values" },
                };
            ops.Add(op);
        }
        var optable = new JsonObject
        {
            ["//"] = "DRAFT. Provider side (upstream/mustContain/drifts) generated from the spec. You must: " +
                     "(1) fill each op's app.method/path from the integration, (2) prune to the ops it implements, " +
                     "(3) set roles + holdout, (4) add deep{} + expectDollars where you want D1/D2 depth.",
            ["roles"] = new JsonObject { ["read"] = "TODO", ["readById"] = "TODO", ["write"] = "TODO", ["unknownIdPath"] = "TODO" },
            ["holdout"] = new JsonObject { ["read"] = "TODO", ["readById"] = "TODO", ["write"] = "TODO", ["write2"] = "TODO", ["secretValue"] = "TODO" },
            ["ops"] = ops,
        };
        Write(outDir, "optable.draft.json", optable);
    }

    // ---- profile.skeleton.json ----------------------------------------------------------------------
    static void WriteProfileSkeleton(OpenApiDocument doc, string outDir, string name)
    {
        var profile = new JsonObject
        {
            ["name"] = name,
            ["app"] = new JsonObject
            {
                ["//"] = "TODO: how to boot the integration under test",
                ["project"] = "",
                ["baseUrl"] = "http://127.0.0.1:5111",
                ["readyPath"] = "/",
                ["routePrefix"] = "TODO (e.g. /api/billing)",
                ["launchArgs"] = new JsonArray("--no-launch-profile"),
                ["config"] = new JsonObject { ["//"] = "TODO: env/config the app boots with; ${mockUrl} points it at the mock. Auth: " + AuthHint(doc) },
                ["secretConfigKeys"] = new JsonArray(),
                ["secretValues"] = new JsonArray(),
            },
            ["mock"] = new JsonObject { ["baseUrl"] = "http://localhost:8080", ["contract"] = "contract.json" },
            ["leak"] = new JsonObject
            {
                ["generic"] = new JsonArray("System.", "Microsoft.", "   at ", ".cs:line", "StackTrace", "Traceback", "Exception", "NullReference"),
                ["extra"] = new JsonArray(),
            },
            ["analysis"] = new JsonObject { ["stack"] = "dotnet", ["integrationPathPattern"] = "TODO", ["depProjectFile"] = "TODO" },
        };
        Write(outDir, "profile.skeleton.json", profile);
    }

    static void PrintChecklist(List<GenOp> gens, List<string> noExample, string outDir)
    {
        var withEx = gens.Count(g => g.Example is not null);
        Console.WriteLine($"\n=== generated into {outDir} ===");
        Console.WriteLine($"operations: {gens.Count}   contract routes (example or schema-synth): {withEx}   no synthesizable body: {noExample.Count}");
        Console.WriteLine("\nfiles:");
        Console.WriteLine("  contract.json          — mock routes+fixtures (provider-faithful; near-final)");
        Console.WriteLine("  optable.draft.json     — ops with upstream/mustContain/drift filled; app-routes + roles are TODO");
        Console.WriteLine("  profile.skeleton.json  — boot/config/leak/analysis are TODO");
        Console.WriteLine("\nremaining (integration-side — the spec cannot know these):");
        Console.WriteLine("  1. optable: set each op's app.method/path (the integration route that makes the call)");
        Console.WriteLine("  2. optable: prune to the ops the integration implements; set roles + holdout");
        Console.WriteLine("  3. optable: add deep{}+expectDollars on ~6-8 representative ops for D1/D2 depth");
        Console.WriteLine("  4. profile: routePrefix, boot config, secretConfigKeys/Values, analysis selectors");
        Console.WriteLine("  5. contract: add 422/reject cases (missing-field, bad-reference) where the gate needs E1/C3");
        if (noExample.Count > 0)
        {
            Console.WriteLine($"\nops with no synthesizable response body (bodyless like DELETE/204, or no schema) — {noExample.Count};");
            Console.WriteLine("add a fixture by hand only if the integration exercises them:");
            foreach (var s in noExample.Take(20)) Console.WriteLine("  - " + s);
            if (noExample.Count > 20) Console.WriteLine($"  … and {noExample.Count - 20} more");
        }
    }

    // ---- parser-agnostic helpers (operate on the JsonNode fixture + path strings) -------------------
    // Unwrap a fixture to the entity object: a single-key envelope ({ "user": {...} }) drops to its
    // inner object; an array takes its first element (itself unwrapped). Otherwise the object as-is.
    static JsonObject? Entity(JsonNode example)
    {
        if (example is JsonObject obj)
            return obj is { Count: 1 } && obj.First().Value is JsonObject inner ? inner : obj;
        if (example is JsonArray arr && arr.FirstOrDefault() is JsonObject e0)
            return e0 is { Count: 1 } && e0.First().Value is JsonObject i2 ? i2 : e0;
        return null;
    }

    static (string? field, string? value) FindPrimaryId(JsonNode example)
    {
        var obj = Entity(example);
        if (obj is null) return (null, null);
        foreach (var key in IdKeys)
            foreach (var kv in obj)
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase) && kv.Value is JsonValue jv)
                    return (kv.Key, jv.ToJsonString().Trim('"'));
        return (null, null);
    }

    // The value a by-id guard should match for a given terminal path param. Prefer the example field whose
    // name equals the param (username → "theUser"); an "*Id"/"id" param keys on the entity's id field
    // (petId, orderId → id). Returns null when the example offers nothing better than the caller's fallback.
    static string? GuardValue(JsonNode example, string paramName)
    {
        var obj = Entity(example);
        if (obj is null) return null;
        foreach (var kv in obj)
            if (string.Equals(kv.Key, paramName, StringComparison.OrdinalIgnoreCase) && kv.Value is JsonValue exact)
                return exact.ToJsonString().Trim('"');
        if (Regex.IsMatch(paramName, @"(^id$|Id$|_id$)", RegexOptions.IgnoreCase))
        {
            var (_, value) = FindPrimaryId(example);
            return value;
        }
        return null;
    }

    static string UpstreamFragment(string path)
    {
        var segs = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var lastLiteral = segs.Length - 1;
        while (lastLiteral >= 0 && segs[lastLiteral].Contains('{')) lastLiteral--;
        return lastLiteral < 0 ? path : "/" + segs[lastLiteral];
    }

    static string LastLiteral(string path)
    {
        var segs = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(s => !s.Contains('{')).ToArray();
        return segs.Length == 0 ? "root" : segs[^1];
    }

    static int StatusInt(string code) => int.TryParse(code, out var i) ? i : 200;

    static string Kebab(string s)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsUpper(c)) { if (i > 0) sb.Append('-'); sb.Append(char.ToLowerInvariant(c)); }
            else if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }

    static void Write(string outDir, string file, JsonNode node) =>
        File.WriteAllText(Path.Combine(outDir, file), node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
}
