namespace MaxioMock;

/// <summary>
/// One inbound request the mock received. This is the gate's window into what the app's
/// integration ACTUALLY did (counts, order, timing, auth) — the basis for the OBS checks
/// (R2/R4/R5/R6/C3/S2) and the upstream-call anti-hardcoding assertions.
/// </summary>
public sealed record RequestRecord(
    long Seq,
    string Method,
    string Path,
    string Query,
    bool HasAuth,
    string? Body,
    long TimestampUnixMs);

/// <summary>Thread-safe in-memory recorder. Reset between gate checks via POST /__mock/reset.</summary>
public sealed class Recorder
{
    private readonly object _gate = new();
    private readonly List<RequestRecord> _records = new();
    private long _seq;

    public void Record(string method, string path, string query, bool hasAuth, string? body)
    {
        lock (_gate)
        {
            _records.Add(new RequestRecord(
                ++_seq, method, path, query, hasAuth, body,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        }
    }

    public IReadOnlyList<RequestRecord> Snapshot()
    {
        lock (_gate) { return _records.ToList(); }
    }

    /// <summary>Count recorded requests whose method matches and whose path contains the fragment.</summary>
    public int Count(string method, string pathContains)
    {
        lock (_gate)
        {
            return _records.Count(r =>
                r.Method.Equals(method, StringComparison.OrdinalIgnoreCase) &&
                r.Path.Contains(pathContains, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void Reset()
    {
        lock (_gate) { _records.Clear(); _seq = 0; }
    }
}
