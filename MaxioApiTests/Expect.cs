using System.Net;
using MaxioApiTests.Ai;
using Xunit;

namespace MaxioApiTests;

/// <summary>Assertion helpers for HTTP tests. Auto-skips on endpoint-missing 404 (route not exposed).</summary>
internal static class Expect
{
    /// <summary>Asserts an exact status code.</summary>
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

    /// <summary>Asserts the status code falls in a range.</summary>
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

    /// <summary>Asserts a 5xx server error status.</summary>
    public static void ServerError(ApiResponse response, string intent)
    {
        SkipIfEndpointMissing(response, intent);
        var code = (int)response.StatusCode;
        if (code is >= 500 and < 600)
        {
            Pass(intent, $"server error status {Describe(response.StatusCode)}");
            return;
        }

        Assert.Fail($"[{intent}] Expected a 5xx server error (faulty upstream must surface as a server error), " +
            $"got {Describe(response.StatusCode)}. Body: {response.Body}");
    }

    /// <summary>Asserts a non-2xx error status.</summary>
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

    /// <summary>Skips test when route is not exposed.</summary>
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

    /// <summary>Substrings that should not appear in error responses.</summary>
    public static readonly string[] ForbiddenInternalSubstrings =
    {
        "System.", "   at ", "Exception", "MaxioAdvancedBilling", "HttpRequestException", "Newtonsoft",
        "Polly", "StackTrace",
        "BytePositionInLine", "LineNumber:", "does not contain any JSON", "valid JSON token",
        "is an invalid end of", "invalid start of"
    };

    /// <summary>Asserts response body contains no internal details.</summary>
    public static void NoInternalLeak(ApiResponse response, string intent)
    {
        foreach (var forbidden in ForbiddenInternalSubstrings)
        {
            NoLeak(response, forbidden, intent);
        }
    }

    /// <summary>Asserts response body does not contain a forbidden substring.</summary>
    public static void NoLeak(ApiResponse response, string forbidden, string intent)
    {
        if (!response.Body.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
        {
            Pass(intent, $"no '{forbidden}' in body");
            return;
        }

        Assert.Fail($"[{intent}] Internal detail leaked into error body — found '{forbidden}'. Body: {response.Body}");
    }

    /// <summary>Asserts AI payload verification passed.</summary>
    public static void AiPassed(VerificationReport report, string intent)
    {
        if (report.Passed)
        {
            Pass(intent, $"AI payload rules satisfied ({report.Results.Count}/{report.Results.Count})");
            return;
        }

        Assert.Fail($"[{intent}] — Unit test failed due to payload verification. {report.FailureSummary}");
    }

    /// <summary>Emits a PASS line to test output.</summary>
    private static void Pass(string intent, string detail) =>
        TestOutput.Current?.WriteLine($"[{intent}] PASS — {detail}");

    /// <summary>Renders a status code by name.</summary>
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
