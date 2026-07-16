namespace Harness.Core;

/// <summary>
/// optable.json — the operations the benchmark exercises, plus the ROLE assignments that let the fixed
/// resilience/hygiene/security check templates (R1–R6, E1–E4, C2/C3, S1–S3) run against this integration
/// without any hard-coded op names. One op table drives Part A (gate C1 + role checks) AND Part B (D1
/// deep-correctness + D2 drift). Authored per integration from the provider contract + the app's routes.
/// </summary>
public sealed class OpTable
{
    public RoleMap Roles { get; init; } = new();
    public HoldoutMap Holdout { get; init; } = new();
    public Op[] Ops { get; init; } = Array.Empty<Op>();

    public Op? ById(string id) => Ops.FirstOrDefault(o => o.Id == id);
}

/// <summary>Public-gate role assignments (op ids). The R/E/S templates resolve these to concrete ops.</summary>
public sealed class RoleMap
{
    /// <summary>A safe, idempotent list/read op (drives R1/R2/R3/R6, S1, S2). Its upstream is fault-injected.</summary>
    public string Read { get; init; } = "";
    /// <summary>A read-by-id op (drives E4 malformed, C2 extra-fields).</summary>
    public string ReadById { get; init; } = "";
    /// <summary>A non-idempotent write op (drives R4 timeout, R5 no-dup, E1 domain-4xx, C3 local-validation).</summary>
    public string Write { get; init; } = "";
    /// <summary>An app path (relative to routePrefix) that acts on a definitely-unknown id (drives E2).</summary>
    public string UnknownIdPath { get; init; } = "";
}

/// <summary>Holdout role assignments — SAME property classes, DIFFERENT concrete instances (never shown to
/// the builder). Reuse of an id is fine as long as the check TYPE applied differs from the public gate.</summary>
public sealed class HoldoutMap
{
    public string Read { get; init; } = "";        // H.R1: transient-5xx recovery on a different read
    public string ReadById { get; init; } = "";     // H.R3: transport-fault wrapping on a read-by-id
    public string Write { get; init; } = "";        // H.E4: malformed tolerated on the WRITE path
    public string Write2 { get; init; } = "";        // H.R5: no-duplicate on a DIFFERENT write
    public string SecretValue { get; init; } = "";   // H.S1: a different secret literal must not be logged
}

public sealed class Op
{
    public string Id { get; init; } = "";
    public AppCall App { get; init; } = new();
    public Upstream Upstream { get; init; } = new();
    /// <summary>Part A C1 expectations (shallow: a genuine call + the key value(s) present).</summary>
    public Expect Gate { get; init; } = new();
    /// <summary>Part B D1 expectations (deeper: id + state + plan, cardinality, units). Defaults to Gate.</summary>
    public Expect? Deep { get; init; }
    /// <summary>D2 drift scenarios applied to this op's upstream, one isolated cell at a time.</summary>
    public DriftCaseSpec[] Drifts { get; init; } = Array.Empty<DriftCaseSpec>();
    /// <summary>Smallest task size that includes this op (0/absent = present in every tree). Lets a
    /// partial integration be scored on the ops it actually implements without penalising absent ops.</summary>
    public int Scope { get; init; }
    /// <summary>For the write role: a body that is locally invalid (missing required field) — C3.</summary>
    public string? InvalidBody { get; init; }
    /// <summary>For the write role: a body the provider rejects with a domain 4xx (bad reference) — E1.</summary>
    public string? DomainErrorBody { get; init; }

    public Expect DeepOrGate => Deep ?? Gate;
}

public sealed class AppCall
{
    public string Method { get; init; } = "GET";
    /// <summary>App route relative to profile.App.RoutePrefix (e.g. "/plans").</summary>
    public string Path { get; init; } = "/";
    /// <summary>Request body for POST/PUT/DELETE happy path (JSON string), or null.</summary>
    public string? Body { get; init; }
}

public sealed class Upstream
{
    public string Method { get; init; } = "GET";
    /// <summary>Wire-path fragment the recorder/fault/drift engines match on (e.g. "products.json").</summary>
    public string PathContains { get; init; } = "";
}

/// <summary>Required-value expectations. MustContain = all present; MustContainAny = each inner group
/// contributes ≥1 (state-representation tolerance: raw wire / SDK enum name / plain English all pass).</summary>
public sealed class Expect
{
    public string[] MustContain { get; init; } = Array.Empty<string>();
    public string[][] MustContainAny { get; init; } = Array.Empty<string[]>();
    /// <summary>Deep-only: a price magnitude that must surface in EITHER cents or dollars form.</summary>
    public double? ExpectDollars { get; init; }
}

/// <summary>One drift scenario. Profile ∈ additive|rename|envelope|retype|union|newenum|remove.
/// Field/To target the wire JSON. Check picks the post-drift oracle; Expect overrides required values.</summary>
public sealed class DriftCaseSpec
{
    public string Label { get; init; } = "";
    public string Profile { get; init; } = "additive";
    public string? Field { get; init; }
    public string? To { get; init; }
    /// <summary>Values | NewEnum | Units — how to classify a 2xx after this drift.</summary>
    public string Check { get; init; } = "Values";
    public string[]? Expect { get; init; }
}
