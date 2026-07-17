using System.Text.Json;
using System.Text.Json.Serialization;

namespace Harness.Core;

/// <summary>
/// profile.json — everything provider/app-specific about *how to run* the integration under test:
/// how to boot it, what config it needs, which values are secrets, what counts as a leak, and which
/// files are "the integration" for static analysis. Authored per integration; consumed by every tool.
/// </summary>
public sealed class Profile
{
    public string Name { get; init; } = "";
    public AppSpec App { get; init; } = new();
    public MockSpec Mock { get; init; } = new();
    public LeakSpec Leak { get; init; } = new();
    public AnalysisSpec Analysis { get; init; } = new();
}

public sealed class AppSpec
{
    /// <summary>Path to the app's csproj (relative to the profile dir or absolute). The default runner
    /// is `dotnet run --project`; override <see cref="RunCommand"/> for non-.NET apps.</summary>
    public string Project { get; init; } = "";
    /// <summary>Optional explicit launch command (tokens), used verbatim instead of `dotnet run` — for
    /// non-.NET stacks. `${appUrl}` / `${mockUrl}` are substituted. Empty ⇒ use the .NET runner.</summary>
    public string[] RunCommand { get; init; } = Array.Empty<string>();
    public string BaseUrl { get; init; } = "http://127.0.0.1:5111";
    /// <summary>Path polled until the app is up (200/any response). Usually "/".</summary>
    public string ReadyPath { get; init; } = "/";
    /// <summary>Prefix prepended to every op's app path (e.g. "/api/billing"). May be "".</summary>
    public string RoutePrefix { get; init; } = "";
    /// <summary>Extra process args (e.g. ["--no-launch-profile"] for ASP.NET launchSettings.json).</summary>
    public string[] LaunchArgs { get; init; } = Array.Empty<string>();
    /// <summary>Extra HTTP headers the harness attaches to EVERY request it sends to the app under test
    /// (all three of gate happy-path/fault drives, holdout, and quality drift replay). Intended for an
    /// integration whose routes require auth — e.g. {"Authorization":"Bearer &lt;jwt&gt;"}. This is test
    /// scaffolding that makes the endpoints reachable; it does NOT change the integration. See PLAYBOOK §5.</summary>
    public Dictionary<string, string> Headers { get; init; } = new();
    /// <summary>Environment/config the app boots with. Values may contain ${mockUrl}/${appUrl}.</summary>
    public Dictionary<string, string> Config { get; init; } = new();
    /// <summary>Config keys removed one-at-a-time for the S3 fail-fast check (each must break boot).</summary>
    public string[] SecretConfigKeys { get; init; } = Array.Empty<string>();
    /// <summary>Secret literals that must never appear in the app's logs (S1) — API key, subdomain, etc.</summary>
    public string[] SecretValues { get; init; } = Array.Empty<string>();
}

public sealed class MockSpec
{
    public string BaseUrl { get; init; } = "http://localhost:8080";
    /// <summary>contract.json filename (relative to the profile dir).</summary>
    public string Contract { get; init; } = "contract.json";
}

/// <summary>Substrings that make any failure body a leak. Generic = stack-independent internals;
/// Extra = provider-specific secrets/tokens. The gate forbids all; the D2 classifier forbids the
/// internals only (a provider wire field-name is a defensible naming choice, not a leak).</summary>
public sealed class LeakSpec
{
    public string[] Generic { get; init; } =
    {
        "System.", "Microsoft.", "   at ", ".cs:line", "StackTrace", "Traceback",
        "Exception", "NullReference",
    };
    public string[] Extra { get; init; } = Array.Empty<string>();
    [JsonIgnore] public string[] All => Generic.Concat(Extra).ToArray();
    /// <summary>Internals-only set for the D2 drift classifier (excludes provider wire field-names).</summary>
    [JsonIgnore] public string[] Internals => Generic
        .Concat(Extra.Where(e => !e.Contains('_')))   // snake_case wire names are not internals
        .ToArray();
}

public sealed class AnalysisSpec
{
    /// <summary>Static-analysis adapter to use. "dotnet" ships; others documented in the playbook.</summary>
    public string Stack { get; init; } = "dotnet";
    /// <summary>Regex matched against each source file's TREE-RELATIVE forward-slashed path (the tree root
    /// is stripped, so the tree's own directory name can't pollute the match); matches select the
    /// integration's OWN code. Non-matching files (host app, tests, generated code) are excluded from
    /// D3/D4 source metrics. e.g. "/(Billing|Provider)" — a Billing/ dir or a Provider-prefixed file.</summary>
    public string IntegrationPathPattern { get; init; } = "";
    /// <summary>Filename of the project/manifest whose dependency graph is counted for D4
    /// (e.g. Infrastructure.csproj, package.json, go.mod). Empty ⇒ the adapter's default discovery.</summary>
    public string DepProjectFile { get; init; } = "";
}
