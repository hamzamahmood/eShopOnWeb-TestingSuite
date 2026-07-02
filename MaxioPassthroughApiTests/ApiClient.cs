using System.Net;
using Xunit.Sdk;

namespace MaxioPassthroughApiTests;

/// <summary>
/// Thin HTTP client for calling the PublicApi over the wire (the curl equivalent, including
/// <c>-k</c> / accept-any-TLS-cert for the local dev certificate). Turns a connection failure into a clear
/// assertion message so a not-running server doesn't look like a mysterious crash.
/// </summary>
public sealed class ApiClient : IDisposable
{
    private readonly HttpClient _http;

    public ApiClient()
    {
        var handler = new HttpClientHandler
        {
            // Equivalent to `curl -k`: trust the local dev HTTPS certificate.
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(TestSettings.BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>GET the given path and read the full response (status, raw body, content-type).</summary>
    public async Task<ApiResponse> GetAsync(string path)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(path);
        }
        catch (HttpRequestException ex)
        {
            throw new XunitException(
                $"Could not reach {TestSettings.BaseUrl}{path}. Is the PublicApi running and routed to the " +
                $"Maxio mock (http://localhost:8080)? See README.md. Underlying error: {ex.Message}");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync();
            return new ApiResponse(response.StatusCode, body, response.Content.Headers.ContentType?.MediaType);
        }
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>A captured HTTP response: status code, raw body text, and content-type.</summary>
public sealed record ApiResponse(HttpStatusCode StatusCode, string Body, string? ContentType);
