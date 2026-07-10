using System.Text.Json.Serialization;

namespace MaxioApiTests.Ai;

/// <summary>AI verification report with per-rule results.</summary>
public sealed class VerificationReport
{
    /// <summary>True only if every supplied rule was satisfied by the payload.</summary>
    public bool Passed { get; set; }

    /// <summary>One entry per rule, in the order the rules were supplied.</summary>
    public List<RuleResult> Results { get; set; } = new();

    /// <summary>Field-level summary of failed rules.</summary>
    [JsonIgnore]
    public string FailureSummary =>
        "Field differences:" + string.Concat(
            Results.Where(r => !r.Passed).Select(r => $"\n - {r.Reason}"));
}

/// <summary>Verification result for a single rule.</summary>
public sealed class RuleResult
{
    public string Rule { get; set; } = "";

    public bool Passed { get; set; }

    public string? Reason { get; set; }
}
