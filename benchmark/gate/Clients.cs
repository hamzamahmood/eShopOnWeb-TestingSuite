using System.Text;
using System.Text.Json;

namespace Gate;

public sealed record ApiResponse(int Status, string Body);

/// <summary>HTTP client for the app under test. Status 0 == the app never responded (crash/hang/refused).</summary>
public sealed class AppClient(string baseUrl)
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(75) };
    private string U(string path) => baseUrl.TrimEnd('/') + path;

    public Task<ApiResponse> Get(string path) => Send(HttpMethod.Get, path, null);
    public Task<ApiResponse> Post(string path, string? json = null) => Send(HttpMethod.Post, path, json);
    public Task<ApiResponse> Put(string path, string? json = null) => Send(HttpMethod.Put, path, json);
    public Task<ApiResponse> Delete(string path, string? json = null) => Send(HttpMethod.Delete, path, json);

    private async Task<ApiResponse> Send(HttpMethod m, string path, string? json)
    {
        using var req = new HttpRequestMessage(m, U(path));
        if (json is not null) req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        try
        {
            using var resp = await _http.SendAsync(req);
            return new ApiResponse((int)resp.StatusCode, await resp.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return new ApiResponse(0, $"CLIENT_ERROR:{ex.GetType().Name}:{ex.Message}");
        }
    }
}

/// <summary>Drives the mock's gate-only control plane (reset / fault config / recordings).</summary>
public sealed class MockClient(string baseUrl)
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public Task Reset() => _http.PostAsync(baseUrl + "/__mock/reset", null);
    public Task Config(string json) => _http.PostAsync(baseUrl + "/__mock/config", new StringContent(json, Encoding.UTF8, "application/json"));

    public async Task<int> Count(string method, string pathContains)
    {
        var recs = await Records();
        return recs.Count(r =>
            string.Equals(r.method, method, StringComparison.OrdinalIgnoreCase) &&
            r.path.Contains(pathContains, StringComparison.OrdinalIgnoreCase));
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
}
