using System.Net;
using MaxioPassthroughApiTests.Ai;
using Xunit;

namespace MaxioPassthroughApiTests;

/// <summary>
/// Uniform assertion helpers for the black-box suite. Every method takes the test's <c>intent</c> — a
/// one-line human description of the behavior under test — and folds it into a single consistent failure
/// message shape: <c>[{intent}] {semantic reason} — {specifics}. Body: {body}</c>, with status codes rendered
/// by name ("404 Not Found"), never bare numbers.
///
/// <para>
/// This replaces ad hoc <c>Assert.True(cond, $"Expected 422, got {(int)code}")</c> messages, which named no
/// intent, printed bare numbers, and sometimes drifted from the condition they described (the old
/// <c>ReactivateSubscriptionTests</c> "Expected 422" message on a check that also accepted 502).
/// </para>
///
/// <para>
/// <b>Route-divergence auto-skip:</b> the three status helpers below first check for an
/// <see cref="TestJson.IsEndpointMissing">endpoint-missing 404</see> (a bare 404 with no JSON error body,
/// meaning this integration does not expose the route) and <c>Skip</c> the test rather than failing it — so
/// a non-exposed route surfaces as a Skipped result, not a false pass/fail. A <b>genuine</b> API 404 (with
/// the app's JSON error body) falls through to the normal comparison, so a test that expected 404 still
/// Passes. Skipping requires the test method to be a <c>[SkippableFact]</c>/<c>[SkippableTheory]</c>.
/// </para>
/// </summary>
internal static class Expect
{
    /// <summary>Asserts a single expected status code (skips on an endpoint-missing 404).</summary>
    public static void Status(ApiResponse response, HttpStatusCode expected, string intent)
    {
        SkipIfEndpointMissing(response, intent);
        if (response.StatusCode == expected)
        {
            Pass(intent, $"status {Describe(expected)}");
            return;
        }

        Assert.Fail($"[{intent}] Incorrect status code received — expected {Describe(expected)}, got " +
            $"{Describe(response.StatusCode)}. Body: {response.Body}");
    }

    /// <summary>Asserts the status code falls in [lo, hiExclusive) — e.g. any 4xx client error (skips on an endpoint-missing 404).</summary>
    public static void StatusInRange(ApiResponse response, int lo, int hiExclusive, string intent, string rangeLabel)
    {
        SkipIfEndpointMissing(response, intent);
        var actual = (int)response.StatusCode;
        if (actual >= lo && actual < hiExclusive)
        {
            Pass(intent, $"status {Describe(response.StatusCode)} in {rangeLabel}");
            return;
        }

        Assert.Fail($"[{intent}] Incorrect status code received — expected {rangeLabel}, got " +
            $"{Describe(response.StatusCode)}. Body: {response.Body}");
    }

    /// <summary>
    /// Asserts the response is NOT a 2xx success — i.e. any error status — skipping on an endpoint-missing
    /// 404 (route not exposed on this integration). Used for failure cases whose exact status legitimately
    /// differs between integrations (Direct maps 404/422/502; Plugin collapses to 422/404/400): the status
    /// gate stays loose while the returned error <b>body</b> is judged for correctness by the LLM verifier
    /// (see <see cref="AiPassed"/>). Because the endpoint-missing skip fires first, an integration whose route
    /// binds the id as <c>int</c> and route-misses a non-numeric id (bare 404) is Skipped, while one that
    /// returns a bodied client error asserts normally.
    /// </summary>
    public static void NotSuccess(ApiResponse response, string intent)
    {
        SkipIfEndpointMissing(response, intent);
        var code = (int)response.StatusCode;
        if (code < 200 || code >= 300)
        {
            Pass(intent, $"error status {Describe(response.StatusCode)}");
            return;
        }

        Assert.Fail($"[{intent}] Expected an error (non-2xx) status, got {Describe(response.StatusCode)}. " +
            $"Body: {response.Body}");
    }

    /// <summary>
    /// Skips the test when the response is an endpoint-missing 404 (route not exposed on this integration),
    /// so route divergence is reported as Skipped rather than a misleading pass/fail. A genuine API 404
    /// (with the app's JSON error body) is left for the caller's normal status assertion.
    /// </summary>
    private static void SkipIfEndpointMissing(ApiResponse response, string intent) =>
        Skip.If(
            TestJson.IsEndpointMissing(response),
            $"[{intent}] Route not exposed on this integration (empty-body 404) — skipped as route divergence.");

    /// <summary>Asserts the response's Content-Type header.</summary>
    public static void ContentType(ApiResponse response, string expected, string intent)
    {
        if (expected == response.ContentType)
        {
            Pass(intent, $"content type '{expected}'");
            return;
        }

        Assert.Fail($"[{intent}] Unexpected response content type — expected '{expected}', got " +
            $"'{response.ContentType ?? "(none)"}'. Body: {response.Body}");
    }

    /// <summary>Asserts any comparable value equals its expectation, e.g. an array length or a JSON value kind.</summary>
    public static void Equal<T>(T expected, T actual, string what, string intent)
    {
        if (Equals(expected, actual))
        {
            Pass(intent, $"{what} == '{expected}'");
            return;
        }

        Assert.Fail($"[{intent}] Unexpected {what} — expected '{expected}', got '{actual}'.");
    }

    /// <summary>Asserts a returned id string is present (non-blank).</summary>
    public static void NonBlankId(string? id, string idKind, string intent)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            Pass(intent, $"{idKind} present");
            return;
        }

        Assert.Fail($"[{intent}] Missing identifier in response — expected a non-blank {idKind}, got " +
            $"'{id}'.");
    }

    /// <summary>Asserts the response body does NOT contain a forbidden internal-detail substring.</summary>
    public static void NoLeak(ApiResponse response, string forbidden, string intent)
    {
        if (!response.Body.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
        {
            Pass(intent, $"no '{forbidden}' in body");
            return;
        }

        Assert.Fail($"[{intent}] Internal detail leaked into error body — found '{forbidden}'. Body: {response.Body}");
    }

    /// <summary>
    /// Asserts an AI payload-verification report passed (every rule satisfied). On failure the message lists
    /// only the differing fields (<c>&lt;field&gt;: missing|mismatched</c>) — no rule text, model reasoning, or
    /// payload values, keeping the report black-box. The AI judges response <b>contents</b>; status codes
    /// remain the job of <see cref="Status"/>.
    /// </summary>
    public static void AiPassed(VerificationReport report, string intent)
    {
        if (report.Passed)
        {
            Pass(intent, $"AI payload rules satisfied ({report.Results.Count}/{report.Results.Count})");
            return;
        }

        Assert.Fail($"[{intent}] — Unit test failed due to payload verification. {report.FailureSummary}");
    }

    /// <summary>
    /// Emits a PASS line for a satisfied assertion to the current test's output — symmetric with the
    /// intent-bearing failure messages above, so a passing test now records what it verified (surfaced in the
    /// JUnit report's <c>&lt;system-out&gt;</c>). No-op when no output helper was captured (see
    /// <see cref="TestOutput"/>).
    /// </summary>
    private static void Pass(string intent, string detail) =>
        TestOutput.Current?.WriteLine($"[{intent}] PASS — {detail}");

    /// <summary>Renders a status code by name, e.g. "404 Not Found" instead of a bare "404".</summary>
    private static string Describe(HttpStatusCode code) => $"{(int)code} {Humanize(code.ToString())}";

    private static string Humanize(string enumName)
    {
        if (enumName == "OK")
        {
            return "OK";
        }

        var chars = new List<char>(enumName.Length + 4);
        foreach (var c in enumName)
        {
            if (char.IsUpper(c) && chars.Count > 0)
            {
                chars.Add(' ');
            }

            chars.Add(c);
        }

        return new string(chars.ToArray());
    }
}
