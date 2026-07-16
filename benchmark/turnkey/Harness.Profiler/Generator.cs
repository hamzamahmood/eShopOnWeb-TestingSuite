using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Harness.Profiler;

/// <summary>
/// Emits a profile DRAFT from an OpenAPI spec. Provider-side fields (mock routes, fixtures, upstream
/// paths, expected id values, drift candidates) are filled from the spec — example-first, so fixtures
/// carry the provider's real envelopes/field-names/types. Integration-side fields the spec cannot know
/// (app-routes, roles, boot config) are emitted as TODO placeholders for the agent to complete.
/// </summary>
public static class Generator
{
    static readonly string[] Methods = { "get", "post", "put", "delete", "patch" };
    static readonly string[] IdKeys = { "id", "uid", "code", "handle", "token", "reference" };
    static int _sentinel;   // deterministic distinct sentinel ids for schema-synthesized fixtures

    public sealed record GenOp(
        string Id, string Method, string Path, string SuccessStatus, JsonNode? Example,
        string? IdField, string? IdValue, string UpstreamFragment, string? RequestBody, bool HasPathParam);

    public static int Run(Spec spec, string outDir, string name)
    {
        Directory.CreateDirectory(outDir);
        _sentinel = 1000001;
        var paths = Spec.Get(spec.Root, "paths");

        var gens = new List<GenOp>();
        var noExample = new List<string>();
        foreach (var (pathKey, pathItem) in Spec.Entries(paths))
            foreach (var (method, opNode) in Spec.Entries(pathItem))
            {
                if (!Methods.Contains(method)) continue;
                var g = Build(spec, pathKey, method, opNode);
                gens.Add(g);
                if (g.Example is null) noExample.Add($"{method.ToUpper()} {pathKey}");
            }

        WriteContract(outDir, gens);
        WriteOptableDraft(outDir, gens);
        WriteProfileSkeleton(spec, outDir, name);
        PrintChecklist(gens, noExample, outDir);
        return 0;
    }

    static GenOp Build(Spec spec, string path, string method, YamlNode opNode)
    {
        var (status, example) = ExtractSuccessExample(spec, opNode);
        var pathParams = Regex.Matches(path, @"\{([^}]+)\}").Select(m => m.Groups[1].Value).ToArray();
        var (idField, idValue) = example is null ? (null, null) : FindPrimaryId(example);
        var opId = Spec.Scalar(Spec.Get(opNode, "operationId")) is { } oid && oid.Length > 0
            ? Kebab(oid) : $"{method}-{LastLiteral(path).TrimEnd('.', 'j', 's', 'o', 'n')}";
        var reqBody = ExtractRequestExample(spec, opNode);
        return new GenOp(opId, method, path, status ?? "200", example, idField, idValue,
            UpstreamFragment(path), reqBody, pathParams.Length > 0);
    }

    // ---- contract.json ------------------------------------------------------------------------------
    static void WriteContract(string outDir, List<GenOp> gens)
    {
        var routes = new JsonArray();
        foreach (var g in gens)
        {
            if (g.Example is null) continue;   // no example ⇒ can't synthesize a body here; listed as TODO
            var cases = new JsonArray();
            // A pathIn guard (known-id → 200, else 404) is only correct for a READ-BY-ID: an idempotent
            // method whose TERMINAL path segment is the id param. Creates (POST) and collection lists
            // (parent param, literal tail) must NOT guard on the returned entity's id.
            var lastSeg = g.Path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
            var terminalParam = Regex.Match(lastSeg, @"\{([^}]+)\}");
            var byId = g.Method is "get" or "delete" or "put" && terminalParam.Success && g.IdValue is not null;
            if (byId)
            {
                cases.Add(new JsonObject
                {
                    ["when"] = new JsonObject { ["pathIn"] = new JsonObject { [terminalParam.Groups[1].Value] = new JsonArray(g.IdValue) } },
                    ["status"] = StatusInt(g.SuccessStatus),
                    ["body"] = g.Example!.DeepClone(),
                });
                cases.Add(new JsonObject { ["status"] = 404 });
            }
            else
            {
                cases.Add(new JsonObject { ["status"] = StatusInt(g.SuccessStatus), ["body"] = g.Example!.DeepClone() });
            }
            routes.Add(new JsonObject { ["method"] = g.Method.ToUpper(), ["path"] = g.Path, ["cases"] = cases });
        }
        var contract = new JsonObject
        {
            ["//"] = "GENERATED from the OpenAPI spec (example-first). Fixtures carry the provider's real shapes. " +
                     "Review error/guard cases (422 on missing fields, bad-reference rejects) — add them per op as needed.",
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
                ["method"] = g.Method.ToUpper(),
                ["path"] = "TODO",
            };
            if (g.RequestBody is not null) app["body"] = g.RequestBody;

            var op = new JsonObject
            {
                ["id"] = g.Id,
                ["scope"] = 0,
                ["app"] = app,
                ["upstream"] = new JsonObject { ["method"] = g.Method.ToUpper(), ["pathContains"] = g.UpstreamFragment },
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
        var todo = new JsonObject { ["//"] = "TODO: pick op ids for each role (see PLAYBOOK §4.3)" };
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
    static void WriteProfileSkeleton(Spec spec, string outDir, string name)
    {
        var authHint = DescribeAuth(spec);
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
                ["config"] = new JsonObject { ["//"] = "TODO: env/config the app boots with; ${mockUrl} points it at the mock. Auth: " + authHint },
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

    // ---- extraction helpers -------------------------------------------------------------------------
    public static (string? status, JsonNode? body) ExtractSuccessExample(Spec spec, YamlNode opNode)
    {
        foreach (var (code, resp) in Spec.Entries(Spec.Get(opNode, "responses")))
        {
            if (!(code.StartsWith("2") || code == "default")) continue;
            var mt = MediaType(Spec.Get(resp, "content"));
            if (mt is null) continue;
            var val = Spec.Get(Spec.Entries(Spec.Get(mt, "examples")).FirstOrDefault().val, "value");
            if (val is not null) return (code, Spec.ToJson(val));
            var example = Spec.Get(mt, "example");
            if (example is not null) return (code, Spec.ToJson(example));
            var schema = Spec.Get(mt, "schema");
            if (schema is not null)
            {
                var synth = SynthFromSchema(spec, schema, spec.Dir, 0, new HashSet<string>());
                if (synth is not null) return (code, synth);
            }
            return (code, null);
        }
        return (null, null);
    }

    static string? ExtractRequestExample(Spec spec, YamlNode opNode)
    {
        var mt = MediaType(Spec.Get(Spec.Get(opNode, "requestBody"), "content"));
        if (mt is null) return null;
        var val = Spec.Get(Spec.Entries(Spec.Get(mt, "examples")).FirstOrDefault().val, "value")
                  ?? Spec.Get(mt, "example");
        return val is null ? null : Spec.ToJson(val)?.ToJsonString();
    }

    static YamlNode? MediaType(YamlNode? content) =>
        Spec.Get(content, "application/json") ?? Spec.Entries(content).Select(e => e.val).FirstOrDefault();

    static (string? field, string? value) FindPrimaryId(JsonNode example)
    {
        // unwrap a single-key envelope ({ "product": {...} }) then look for an id-ish scalar
        JsonObject? obj = example as JsonObject;
        if (obj is { Count: 1 } && obj.First().Value is JsonObject inner) obj = inner;
        else if (example is JsonArray arr && arr.FirstOrDefault() is JsonObject e0)
            obj = e0.Count == 1 && e0.First().Value is JsonObject i2 ? i2 : e0;
        if (obj is null) return (null, null);
        foreach (var key in IdKeys)
            foreach (var kv in obj)
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase) && kv.Value is JsonValue jv)
                    return (kv.Key, jv.ToJsonString().Trim('"'));
        return (null, null);
    }

    static string UpstreamFragment(string path)
    {
        // the literal tail: drop trailing {param} segments, keep from the last literal segment
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

    static string DescribeAuth(Spec spec)
    {
        var schemes = Spec.Get(Spec.Get(spec.Root, "components"), "securitySchemes");
        var names = Spec.Entries(schemes).Select(e =>
        {
            var t = Spec.Scalar(Spec.Get(e.val, "type"));
            var scheme = Spec.Scalar(Spec.Get(e.val, "scheme"));
            return $"{e.key} ({t}{(scheme is null ? "" : "/" + scheme)})";
        }).ToList();
        return names.Count == 0 ? "none declared" : string.Join(", ", names);
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

    // ---- schema-based fixture synthesis (fallback when an operation has no inline example) ----------
    // Walks a (deref'd) OpenAPI schema and builds a structurally-faithful JSON value: objects from
    // properties, arrays from items, enums→first value, id-ish integers→distinct sentinels. Depth-capped
    // and $ref-cycle-guarded so recursive Maxio schemas terminate.
    static JsonNode? SynthFromSchema(Spec spec, YamlNode schemaNode, string baseDir, int depth, HashSet<string> seen)
    {
        if (depth > 6) return null;
        if (Spec.Get(schemaNode, "$ref") is YamlScalarNode { Value: { } r } && !seen.Add(baseDir + "|" + r))
            return null;   // cycle
        var (s, nb) = spec.Deref(schemaNode, baseDir);

        if (Spec.Get(s, "example") is { } ex) return Spec.ToJson(ex);

        if (Spec.Get(s, "allOf") is YamlSequenceNode allOf)
        {
            var merged = new JsonObject();
            foreach (var sub in allOf.Children)
                if (SynthFromSchema(spec, sub, nb, depth, new HashSet<string>(seen)) is JsonObject vo)
                    foreach (var name in vo.Select(kv => kv.Key).ToList()) { var v = vo[name]; vo.Remove(name); merged[name] = v; }
            MergeProps(spec, s, nb, merged, depth, seen);
            return merged;
        }
        foreach (var key in new[] { "oneOf", "anyOf" })
            if (Spec.Get(s, key) is YamlSequenceNode { Children.Count: > 0 } seq)
                return SynthFromSchema(spec, seq.Children[0], nb, depth, seen);

        var type = SchemaType(s);
        if (type == "object" || Spec.Get(s, "properties") is not null)
        {
            var obj = new JsonObject();
            MergeProps(spec, s, nb, obj, depth, seen);
            return obj;
        }
        if (type == "array")
        {
            var arr = new JsonArray();
            if (Spec.Get(s, "items") is { } items && SynthFromSchema(spec, items, nb, depth + 1, seen) is { } iv) arr.Add(iv);
            return arr;
        }
        return ScalarSynth(type, s);
    }

    static void MergeProps(Spec spec, YamlNode s, string baseDir, JsonObject obj, int depth, HashSet<string> seen)
    {
        foreach (var (name, propSchema) in Spec.Entries(Spec.Get(s, "properties")))
            obj[name] = SynthFromSchema(spec, propSchema, baseDir, depth + 1, new HashSet<string>(seen));
    }

    static string? SchemaType(YamlNode s) => Spec.Get(s, "type") switch
    {
        YamlScalarNode sc => sc.Value,
        YamlSequenceNode seq => seq.Children.OfType<YamlScalarNode>().Select(x => x.Value).FirstOrDefault(v => v != "null"),
        _ => null,
    };

    static JsonNode? ScalarSynth(string? type, YamlNode s)
    {
        if (Spec.Get(s, "enum") is YamlSequenceNode { Children.Count: > 0 } en) return Spec.ToJson(en.Children[0]);
        switch (type)
        {
            case "integer": return JsonValue.Create(_sentinel++);
            case "number": return JsonValue.Create((double)_sentinel++);
            case "boolean": return JsonValue.Create(false);
            case "string":
                return JsonValue.Create(Spec.Scalar(Spec.Get(s, "format")) switch
                {
                    "date" => "2024-01-01",
                    "date-time" => "2024-01-01T00:00:00Z",
                    "email" => "user@example.com",
                    "uuid" => "00000000-0000-0000-0000-000000000000",
                    "uri" => "https://example.com",
                    _ => "string",
                });
            default: return null;
        }
    }

    static void Write(string outDir, string file, JsonNode node) =>
        File.WriteAllText(Path.Combine(outDir, file), node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
}
