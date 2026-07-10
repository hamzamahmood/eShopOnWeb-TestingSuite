using System.Net;
using System.Text.Json;

namespace MaxioApiTests;

/// <summary>Helpers for classifying HTTP 404 responses.</summary>
internal static class TestJson
{
    /// <summary>Checks if response is a genuine API 404 (has JSON error body).</summary>
    public static bool IsApiNotFound(ApiResponse response) =>
        response.StatusCode == HttpStatusCode.NotFound
        && response.ContentType is not null
        && response.ContentType.Contains("application/json", StringComparison.OrdinalIgnoreCase)
        && ParsesAsJsonObject(response.Body);

    /// <summary>Checks if response is an endpoint-missing 404 (no route match).</summary>
    public static bool IsEndpointMissing(ApiResponse response) =>
        response.StatusCode == HttpStatusCode.NotFound && !IsApiNotFound(response);

    private static bool ParsesAsJsonObject(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
