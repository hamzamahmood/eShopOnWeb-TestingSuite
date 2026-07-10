using System.Net;
using System.Text.Json;

namespace MaxioApiTests;

/// <summary>
/// Helpers for classifying HTTP 404 responses: distinguishing a <b>genuine API 404</b> (the app's JSON error
/// body) from an <b>endpoint-missing 404</b> (route not exposed on this integration), which drives the
/// route-divergence auto-skip in <see cref="Expect"/>. (Payload field/id/state comparisons are now handled by
/// the AI verifier in <see cref="Ai.OpenAIApiService"/>, so the former tolerant JSON readers were removed.)
/// </summary>
internal static class TestJson
{
    /// <summary>
    /// A <b>genuine API 404</b>: the controller/middleware deliberately returned not-found. It carries the
    /// app's <c>application/json</c> error body (<c>{"StatusCode":404,"Message":"…"}</c> on both integrations),
    /// as opposed to an <see cref="IsEndpointMissing">endpoint-missing 404</see>.
    /// </summary>
    public static bool IsApiNotFound(ApiResponse response) =>
        response.StatusCode == HttpStatusCode.NotFound
        && response.ContentType is not null
        && response.ContentType.Contains("application/json", StringComparison.OrdinalIgnoreCase)
        && ParsesAsJsonObject(response.Body);

    /// <summary>
    /// An <b>endpoint-missing 404</b>: no controller route matched, so ASP.NET returned a bare 404 with an
    /// empty body and no <c>application/json</c> content type. Used to skip (rather than fail) tests whose
    /// target route this integration simply does not expose. NOTE: a deliberate bare <c>NotFound()</c> with no
    /// body (e.g. the Plugin's <c>customers/lookup</c> on an unknown reference) is indistinguishable from a
    /// routing 404 by body alone and is therefore treated as endpoint-missing too — see README.
    /// </summary>
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
