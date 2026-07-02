using System.Net;
using System.Net.Http.Json;
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
    public Task<ApiResponse> GetAsync(string path) => SendAsync(() => _http.GetAsync(path), HttpMethod.Get, path);

    /// <summary>POST <paramref name="body"/> (serialized as JSON, or no body when null) to the given path.</summary>
    public Task<ApiResponse> PostAsync(string path, object? body = null) =>
        SendAsync(() => _http.PostAsync(path, JsonBody(body)), HttpMethod.Post, path);

    /// <summary>PUT <paramref name="body"/> (serialized as JSON, or no body when null) to the given path.</summary>
    public Task<ApiResponse> PutAsync(string path, object? body = null) =>
        SendAsync(() => _http.PutAsync(path, JsonBody(body)), HttpMethod.Put, path);

    /// <summary>DELETE the given path, optionally with a JSON body.</summary>
    public Task<ApiResponse> DeleteAsync(string path, object? body = null)
    {
        return SendAsync(async () =>
        {
            // The `using` must wrap the await, not just the SendAsync call: SendAsync returns before the
            // request body has necessarily finished being read, so disposing `request` (and its Content)
            // synchronously right after invoking it — rather than after it completes — races the in-flight
            // send and previously surfaced as a spurious HttpRequestException on every DELETE-with-body call.
            using var request = new HttpRequestMessage(HttpMethod.Delete, path);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            return await _http.SendAsync(request);
        }, HttpMethod.Delete, path);
    }

    private static HttpContent? JsonBody(object? body) => body is null ? null : JsonContent.Create(body);

    private async Task<ApiResponse> SendAsync(Func<Task<HttpResponseMessage>> send, HttpMethod method, string path)
    {
        HttpResponseMessage response;
        try
        {
            response = await send();
        }
        catch (HttpRequestException ex)
        {
            throw new XunitException(
                $"Could not {method} {TestSettings.BaseUrl}{path}. Is the PublicApi running and routed to the " +
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
