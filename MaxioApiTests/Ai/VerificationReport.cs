using System.Text.Json.Serialization;

namespace MaxioApiTests.Ai;

/// <summary>
/// The structured verdict returned by <see cref="OpenAIApiService.VerifyAsync"/>. The AI model is forced
/// (via JSON-schema-constrained structured output) to populate exactly this shape — one <see cref="RuleResult"/>
/// per rule the test supplied — so no free-text parsing is needed on the C# side.
/// </summary>
public sealed class VerificationReport
{
    /// <summary>True only if every supplied rule was satisfied by the payload.</summary>
    public bool Passed { get; set; }

    /// <summary>One entry per rule, in the order the rules were supplied.</summary>
    public List<RuleResult> Results { get; set; } = new();

    /// <summary>
    /// Field-level summary of the failed rules for the assertion failure message — one bulleted
    /// <c>&lt;field&gt;: missing|mismatched</c> line per failure, with no rule text, model reasoning, or
    /// payload values (the model is instructed to put only that into each <see cref="RuleResult.Reason"/>, so
    /// the black-box report never leaks context). Excluded from the JSON schema/serialization so the model is
    /// never asked to fill it.
    /// </summary>
    [JsonIgnore]
    public string FailureSummary =>
        "Field differences:" + string.Concat(
            Results.Where(r => !r.Passed).Select(r => $"\n - {r.Reason}"));
}

/// <summary>The model's verdict on a single rule.</summary>
public sealed class RuleResult
{
    /// <summary>The rule text, echoed back so failures are self-describing.</summary>
    public string Rule { get; set; } = "";

    /// <summary>Whether the payload satisfies this rule.</summary>
    public bool Passed { get; set; }

    /// <summary>A short, concrete reason — required when <see cref="Passed"/> is false.</summary>
    public string? Reason { get; set; }
}
