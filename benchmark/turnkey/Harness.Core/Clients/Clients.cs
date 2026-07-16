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
public sealed class AppClient(string baseUrl)
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

    private async Task<ApiResponse> Send(HttpMethod m, string path, string? json)
    {
        using var req = new HttpRequestMessage(m, U(path));
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
