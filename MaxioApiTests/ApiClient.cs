using System.Net;
using System.Net.Http.Json;
using Xunit.Sdk;

namespace MaxioApiTests;

/// <summary>HTTP client for calling the API under test.</summary>
public sealed class ApiClient : IDisposable
{
    private readonly HttpClient _http;

    public ApiClient()
    {
        var handler = new HttpClientHandler
        {
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
                $"Could not {method} {TestSettings.BaseUrl}{path}. Is the API running? Underlying error: {ex.Message}");
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
