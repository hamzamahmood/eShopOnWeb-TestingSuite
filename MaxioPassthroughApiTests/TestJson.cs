using System.Linq;
using System.Net;
using System.Text.Json;

namespace MaxioPassthroughApiTests;

/// <summary>
/// Tolerant JSON readers for fields that exist on BOTH integrations' response DTOs but under different
/// property names, types, or root shapes (see docs/maxio-billing-controller-comparison.md). Centralizing
/// these keeps each Tests/*.cs file asserting business meaning ("the returned id"), not per-integration
/// plumbing.
/// </summary>
internal static class TestJson
{
    /// <summary>Subscription id: Direct's "providerSubscriptionId" (int) or Plugin's "subscriptionId" (string).</summary>
    public static string GetSubscriptionId(JsonElement subscription) =>
        subscription.TryGetProperty("subscriptionId", out var s)
            ? s.GetString()!
            : subscription.GetProperty("providerSubscriptionId").GetRawText();

    /// <summary>Usage id: Direct's "providerUsageId" (long) or Plugin's "usageId" (string).</summary>
    public static string GetUsageId(JsonElement usage) =>
        usage.TryGetProperty("usageId", out var s)
            ? s.GetString()!
            : usage.GetProperty("providerUsageId").GetRawText();

    /// <summary>
    /// Customer id from the find-or-create response: Direct's controller returns a bare number
    /// (<c>Ok(int)</c>); Plugin returns <c>{"customerId": "..."}</c>.
    /// </summary>
    public static string GetCustomerId(JsonElement root) =>
        root.ValueKind == JsonValueKind.Number
            ? root.GetRawText()
            : root.GetProperty("customerId").GetString()!;

    /// <summary>
    /// Compares a subscription "state" value case- and separator-insensitively: Direct forwards Maxio's raw
    /// snake_case string (e.g. "on_hold") verbatim, while Plugin renders its <c>SubscriptionState</c> enum by
    /// member name (e.g. "OnHold"). Plain case-insensitive comparison is NOT enough for multi-word states —
    /// "on_hold" and "onhold" differ only by the underscore — so both sides are stripped of non-letters first.
    /// </summary>
    public static bool StatesEqual(string expected, string actual) =>
        string.Equals(Normalize(expected), Normalize(actual), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value) => new(value.Where(char.IsLetter).ToArray());

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
