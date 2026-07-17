using System.Text;
using System.Text.Json;

namespace Harness.Core;

/// <summary>A response from the app under test. Status 0 == no response (crash/hang/refused).</summary>
public sealed record ApiResponse(int Status, string Body)
{
    public bool Ok => Status is >= 200 and < 300;
    public bool Is4xx => Status is >= 400 and < 500;
    public bool Is5xx => Status is >= 500 and < 600;
    public bool Crashed => Status == 0;                     // never responded (crash/hang/refused)
    public bool Has(string v) => Body.Contains(v, StringComparison.OrdinalIgnoreCase);
    public bool HasAll(IEnumerable<string> vs) => vs.All(Has);
    public bool HasAnyOf(IEnumerable<string> vs) => vs.Any(Has);
}

/// <summary>
/// Black-box HTTP client for the app under test. Everything the benchmark asserts is observed through
/// this client (status ranges, value-presence, timing) — never by reading the integration's source.
/// A 75s timeout means a genuinely hanging integration surfaces as Status 0 rather than deadlocking a run.
/// </summary>
public sealed class AppClient(string baseUrl, IReadOnlyDictionary<string, string>? headers = null)
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(75) };
    private string U(string path) => baseUrl.TrimEnd('/') + path;

    public Task<ApiResponse> Get(string path) => Send(HttpMethod.Get, path, null);
    public Task<ApiResponse> Post(string path, string? json = null) => Send(HttpMethod.Post, path, json);
    public Task<ApiResponse> Put(string path, string? json = null) => Send(HttpMethod.Put, path, json);
    public Task<ApiResponse> Delete(string path, string? json = null) => Send(HttpMethod.Delete, path, json);

    public Task<ApiResponse> Call(string method, string path, string? json) => method.ToUpperInvariant() switch
    {
        "GET" => Get(path),
        "POST" => Post(path, json),
        "PUT" => Put(path, json),
        "DELETE" => Delete(path, json),
        "PATCH" => Send(HttpMethod.Patch, path, json),
        _ => Get(path),
    };

    /// <summary>Drive one operation, running its <see cref="AppCall.PreSteps"/> first (capturing named
    /// values from their responses) and interpolating {{capture.X}} into the op's path/body. With no
    /// preSteps this is exactly <c>Call(method, prefix+path, body)</c> — the single-shot path is unchanged.
    /// <paramref name="bodyOverride"/> replaces the op body (for C3/E1 invalid/domain-error drives) and is
    /// itself interpolated so an override can still reference captures.</summary>
    public async Task<ApiResponse> DriveOp(string prefix, AppCall call, string? bodyOverride = null)
    {
        var caps = new Dictionary<string, string>();
        foreach (var step in call.PreSteps)
        {
            var sr = await Call(step.Method, prefix + Interpolate(step.Path, caps),
                                step.Body is null ? null : Interpolate(step.Body, caps));
            foreach (var (name, jsonPath) in step.Capture)
                if (ExtractJson(sr.Body, jsonPath) is { } v) caps[name] = v;
        }
        var body = bodyOverride ?? call.Body;
        return await Call(call.Method, prefix + Interpolate(call.Path, caps),
                          body is null ? null : Interpolate(body, caps));
    }

    /// <summary>Replace {{capture.NAME}} tokens with captured values (missing ⇒ left verbatim). A string
    /// with no such token — every op authored before preSteps existed — is returned unchanged.</summary>
    private static string Interpolate(string s, IReadOnlyDictionary<string, string> caps)
    {
        if (caps.Count == 0 || !s.Contains("{{capture.")) return s;
        foreach (var (k, v) in caps) s = s.Replace("{{capture." + k + "}}", v);
        return s;
    }

    /// <summary>Extract a scalar from a JSON body by a dotted path; numeric segments index arrays.
    /// Returns null on any parse/traversal miss (the caller then simply skips that capture).</summary>
    private static string? ExtractJson(string body, string dotted)
    {
        try
        {
            var el = JsonDocument.Parse(body).RootElement;
            foreach (var seg in dotted.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (el.ValueKind == JsonValueKind.Array && int.TryParse(seg, out var idx))
                {
                    if (idx < 0 || idx >= el.GetArrayLength()) return null;
                    el = el[idx];
                }
                else if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(seg, out var next))
                    el = next;
                else return null;
            }
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Null => null,
                _ => el.GetRawText(),
            };
        }
        catch { return null; }
    }

    private async Task<ApiResponse> Send(HttpMethod m, string path, string? json)
    {
        using var req = new HttpRequestMessage(m, U(path));
        if (headers is not null)
            foreach (var (k, v) in headers) req.Headers.TryAddWithoutValidation(k, v);
        if (json is not null) req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        try
        {
            using var resp = await _http.SendAsync(req);
            return new ApiResponse((int)resp.StatusCode, await resp.Content.ReadAsStringAsync());
        }
        catch (Exception ex) { return new ApiResponse(0, $"CLIENT_ERROR:{ex.GetType().Name}:{ex.Message}"); }
    }

    /// <summary>Recursively collect every numeric value in a JSON body (JSON numbers + numeric strings).
    /// Used by the deep-correctness unit check (e.g. cents-vs-dollars magnitude).</summary>
    public static IReadOnlyList<double> Numbers(string body)
    {
        var nums = new List<double>();
        try { Walk(JsonDocument.Parse(body).RootElement, nums); } catch { }
        return nums;
    }

    private static void Walk(JsonElement e, List<double> acc)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Number: if (e.TryGetDouble(out var d)) acc.Add(d); break;
            case JsonValueKind.String:
                if (double.TryParse(e.GetString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var s)) acc.Add(s);
                break;
            case JsonValueKind.Object: foreach (var p in e.EnumerateObject()) Walk(p.Value, acc); break;
            case JsonValueKind.Array: foreach (var i in e.EnumerateArray()) Walk(i, acc); break;
        }
    }
}

/// <summary>Drives the mock's gate-only control plane (reset / fault + drift config / recordings).</summary>
public sealed class MockClient(string baseUrl)
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public Task Reset() => _http.PostAsync(baseUrl + "/__mock/reset", null);
    public Task Config(string json) => _http.PostAsync(baseUrl + "/__mock/config",
        new StringContent(json, Encoding.UTF8, "application/json"));

    /// <summary>Install a single fault rule scoped to an upstream wire-path fragment.</summary>
    public Task Fault(string pathContains, string? method, string action, int times, int retryAfter = 0)
    {
        var m = method is null ? "" : $"\"method\":\"{method}\",";
        return Config($"{{\"faults\":[{{\"pathContains\":\"{Esc(pathContains)}\",{m}\"action\":\"{action}\",\"times\":{times},\"retryAfter\":{retryAfter}}}]}}");
    }

    /// <summary>Install a single drift rule scoped to an upstream wire-path fragment.</summary>
    public Task Drift(string pathContains, string? method, string profile, string? field, string? to)
    {
        var m = method is null ? "" : $"\"method\":\"{method}\",";
        var f = field is null ? "" : $"\"field\":\"{Esc(field)}\",";
        var t = to is null ? "" : $"\"to\":\"{Esc(to)}\",";
        return Config($"{{\"drift\":[{{\"pathContains\":\"{Esc(pathContains)}\",{m}{f}{t}\"profile\":\"{profile}\"}}]}}");
    }

    public Task RequireAuth() => Config("{\"requireAuth\":true}");

    public async Task<int> Count(string method, string pathContains)
    {
        var recs = await Records();
        return recs.Count(r => string.Equals(r.method, method, StringComparison.OrdinalIgnoreCase)
                            && r.path.Contains(pathContains, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<int> Total() => (await Records()).Count;

    private async Task<List<(string method, string path)>> Records()
    {
        var body = await _http.GetStringAsync(baseUrl + "/__mock/recordings");
        using var doc = JsonDocument.Parse(body);
        var list = new List<(string, string)>();
        foreach (var r in doc.RootElement.GetProperty("records").EnumerateArray())
            list.Add((r.GetProperty("Method").GetString() ?? "", r.GetProperty("Path").GetString() ?? ""));
        return list;
    }

    private static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
