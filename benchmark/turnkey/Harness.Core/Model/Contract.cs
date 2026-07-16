using System.Text.Json;

namespace Harness.Core;

/// <summary>
/// contract.json — a DECLARATIVE description of the provider's wire API, served by the generic mock.
/// Authored from the provider's OpenAPI spec / docs (NOT from the integration under test — that would
/// make the oracle circular). Each route is matched by method + path template; its ordered cases are
/// evaluated top-to-bottom and the first whose guard holds produces the response. A case body is a raw
/// JSON value or a named fixture; both support {{path.x}} / {{query.x}} / {{body.a.b}} interpolation.
///
/// This covers the common REST/JSON case (static fixtures + known-id/404 + missing-field/422 +
/// bad-reference/422 + created-id + echo). Providers needing computed/stateful responses use the C#
/// escape hatch (IProviderContract) documented in the playbook.
/// </summary>
public sealed class Contract
{
    /// <summary>Named JSON fixtures a route case can return via "bodyFixture". Raw provider-shaped JSON.</summary>
    public Dictionary<string, JsonElement> Fixtures { get; init; } = new();
    public RouteDef[] Routes { get; init; } = Array.Empty<RouteDef>();
    /// <summary>Request header names that count as "authenticated" for the S2 auth-applied check. The
    /// provider may key auth off a custom header (e.g. "api_key", "X-Api-Key") rather than "Authorization".
    /// Null/empty ⇒ ["Authorization"], so an Authorization-bearing integration is unaffected.</summary>
    public string[]? AuthHeaders { get; init; }
}

public sealed class RouteDef
{
    public string Method { get; init; } = "GET";
    /// <summary>Path template with {param} captures, e.g. "/subscriptions/{sid}.json".</summary>
    public string Path { get; init; } = "/";
    public CaseDef[] Cases { get; init; } = Array.Empty<CaseDef>();
}

public sealed class CaseDef
{
    /// <summary>Guard; null ⇒ always matches (catch-all/default). All present conditions must hold.</summary>
    public GuardSpec? When { get; init; }
    public int Status { get; init; } = 200;
    /// <summary>Inline JSON body (object/array/string/number). Mutually exclusive with BodyFixture.</summary>
    public JsonElement? Body { get; init; }
    /// <summary>Name of a fixture in Contract.Fixtures to serve.</summary>
    public string? BodyFixture { get; init; }
    public string ContentType { get; init; } = "application/json";
}

/// <summary>
/// A case guard. Every specified condition must hold for the case to match. Path/query/body paths use
/// dot notation (body only). "*Missing"/"*NotIn" express the reject branches (missing field → 422,
/// bad reference → 422).
/// </summary>
public sealed class GuardSpec
{
    public Dictionary<string, string[]>? PathIn { get; init; }
    public string[]? QueryPresent { get; init; }
    public Dictionary<string, string[]>? QueryIn { get; init; }
    /// <summary>These body paths must exist AND be non-empty (present-field branch).</summary>
    public string[]? BodyPresent { get; init; }
    /// <summary>Matches when ANY listed body path is absent/empty (missing-field reject branch).</summary>
    public string[]? BodyMissing { get; init; }
    public Dictionary<string, string[]>? BodyValueIn { get; init; }
    /// <summary>Matches when the body path's value is NOT in the set (bad-reference reject branch).</summary>
    public Dictionary<string, string[]>? BodyValueNotIn { get; init; }
}
