namespace Harness.Core;

/// <summary>
/// A fault rule the gate installs (via POST /__mock/config) before a resilience/hygiene check.
/// Matches inbound provider requests by method and/or a path fragment (the upstream wire path, which
/// the op table knows for each op), and applies <see cref="Action"/> to the first <see cref="Times"/>
/// matches — then passes through. That "first N then succeed" shape is what tests 503-then-200,
/// 429-then-200, etc. Reset the counters via POST /__mock/reset.
///
/// PROVIDER-AGNOSTIC: faults are transport/status-level and say nothing about any particular API.
/// </summary>
public sealed record FaultRule(string? PathContains, string? Method, string Action, int Times, int RetryAfter);

public sealed class FaultEngine
{
    private readonly object _gate = new();
    private List<FaultRule> _rules = new();
    private readonly Dictionary<int, int> _hits = new();

    public void SetRules(IEnumerable<FaultRule>? rules)
    {
        lock (_gate) { _rules = rules?.ToList() ?? new(); _hits.Clear(); }
    }

    public void Reset()
    {
        lock (_gate) { _rules.Clear(); _hits.Clear(); }
    }

    /// <summary>Returns the fault to apply for this request, or null to pass through to the route.</summary>
    public (string Action, int RetryAfter)? Match(string method, string path)
    {
        lock (_gate)
        {
            for (var i = 0; i < _rules.Count; i++)
            {
                var r = _rules[i];
                if (r.Method is not null && !r.Method.Equals(method, StringComparison.OrdinalIgnoreCase)) continue;
                if (r.PathContains is not null && !path.Contains(r.PathContains, StringComparison.OrdinalIgnoreCase)) continue;
                var used = _hits.GetValueOrDefault(i);
                if (used >= r.Times) continue;      // exhausted -> pass through
                _hits[i] = used + 1;
                return (r.Action, r.RetryAfter);
            }
            return null;
        }
    }
}
