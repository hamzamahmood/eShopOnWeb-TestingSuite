using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Reference;

public sealed class MaxioSettings
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Subdomain { get; set; } = "";
    public int ProductFamilyId { get; set; }
    public string ProductFamilyHandle { get; set; } = "";
    public int MeteredComponentId { get; set; }
    public string MeteredComponentHandle { get; set; } = "";
}

/// <summary>Deliberate-defect toggles (env BREAK=...), used to prove the gate discriminates.</summary>
public sealed record Breaks(bool Leak, bool RetryWrite, bool NoTimeout, bool Raw500, bool NoAuth, bool LogSecret, bool Hardcode, bool NaiveErrors)
{
    public static Breaks From(string? env)
    {
        var s = (env ?? "").ToLowerInvariant();
        bool H(string k) => s.Contains(k);
        return new Breaks(H("leak"), H("retrywrite"), H("notimeout"), H("raw500"), H("noauth"), H("logsecret"), H("hardcode"), H("naiveerrors"));
    }
}

public sealed class NotFoundException(string message) : Exception(message);
public sealed class ValidationFailedException(IReadOnlyList<string> errors)
    : Exception(string.Join("; ", errors)) { public IReadOnlyList<string> Errors { get; } = errors; }
public sealed class ProviderRejectedException(string message) : Exception(message);
public sealed class ProviderUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public sealed record PlanDto(long Id, string Name, string Handle, int PriceInCents);
public sealed record CustomerDto(long Id, string? Reference);
public sealed record SubscriptionDto(long Id, string State, string? PlanHandle);
public sealed record UsageDto(long Id, double Quantity);

/// <summary>
/// A correct raw-HttpClient Maxio client: hand-rolled resilience (retry idempotent GETs only —
/// NEVER writes — with a per-attempt timeout), transport/timeout errors wrapped into domain
/// exceptions, and Maxio error-shape parsing. This is what the SDK gives Arm A "for free".
/// </summary>
public sealed class MaxioClient(HttpClient http, MaxioSettings s, Breaks breaks)
{
    private static readonly JsonSerializerOptions JsonOut = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<IReadOnlyList<PlanDto>> ListPlansAsync(CancellationToken ct)
    {
        var (status, body) = await Send(HttpMethod.Get, $"/product_families/{s.ProductFamilyId}/products.json", null, ct);
        EnsureOk(status, body);
        using var doc = JsonDocument.Parse(body);
        var list = new List<PlanDto>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var p = item.GetProperty("product");
            list.Add(new PlanDto(GetLong(p, "id"), GetStr(p, "name") ?? "", GetStr(p, "handle") ?? "", GetInt(p, "price_in_cents")));
        }
        return list;
    }

    public async Task<CustomerDto> FindOrCreateCustomerAsync(string reference, string firstName, string lastName, string email, CancellationToken ct)
    {
        var (ls, lb) = await Send(HttpMethod.Get, $"/customers/lookup.json?reference={Uri.EscapeDataString(reference)}", null, ct);
        if (ls == HttpStatusCode.OK)
        {
            using var d = JsonDocument.Parse(lb);
            var c = d.RootElement.GetProperty("customer");
            return new CustomerDto(GetLong(c, "id"), GetStr(c, "reference"));
        }
        if (ls != HttpStatusCode.NotFound) EnsureOk(ls, lb);
        var envelope = new { customer = new { first_name = firstName, last_name = lastName, email, reference } };
        var (cs, cb) = await Send(HttpMethod.Post, "/customers.json", envelope, ct);
        EnsureOk(cs, cb);
        using var cd = JsonDocument.Parse(cb);
        var cc = cd.RootElement.GetProperty("customer");
        return new CustomerDto(GetLong(cc, "id"), GetStr(cc, "reference"));
    }

    public async Task<IReadOnlyList<SubscriptionDto>> ListSubscriptionsAsync(long customerId, CancellationToken ct)
    {
        var (status, body) = await Send(HttpMethod.Get, $"/customers/{customerId}/subscriptions.json", null, ct);
        EnsureOk(status, body);
        using var doc = JsonDocument.Parse(body);
        var list = new List<SubscriptionDto>();
        foreach (var item in doc.RootElement.EnumerateArray())
            list.Add(MapSub(item.GetProperty("subscription")));
        return list;
    }

    public Task<SubscriptionDto> ReadSubscriptionAsync(long id, CancellationToken ct)
        => OneSub(HttpMethod.Get, $"/subscriptions/{id}.json", null, ct);
    public Task<SubscriptionDto> PauseAsync(long id, CancellationToken ct)
        => OneSub(HttpMethod.Post, $"/subscriptions/{id}/hold.json", new { hold = new { } }, ct);
    public Task<SubscriptionDto> ResumeAsync(long id, CancellationToken ct)
        => OneSub(HttpMethod.Post, $"/subscriptions/{id}/resume.json", null, ct);
    public Task<SubscriptionDto> ReactivateAsync(long id, CancellationToken ct)
        => OneSub(HttpMethod.Put, $"/subscriptions/{id}/reactivate.json", null, ct);
    public Task<SubscriptionDto> CreateSubscriptionAsync(string customerReference, string productHandle, CancellationToken ct)
        => OneSub(HttpMethod.Post, "/subscriptions.json", new { subscription = new { product_handle = productHandle, customer_reference = customerReference } }, ct);
    public Task<SubscriptionDto> PlanChangeAsync(long id, string productHandle, CancellationToken ct)
        => OneSub(HttpMethod.Post, $"/subscriptions/{id}/migrations.json", new { migration = new { product_handle = productHandle } }, ct);
    public Task<SubscriptionDto> CancelAsync(long id, CancellationToken ct)
        => OneSub(HttpMethod.Delete, $"/subscriptions/{id}.json", new { subscription = new { cancellation_message = "requested" } }, ct);

    public async Task<UsageDto> RecordUsageAsync(long id, double quantity, string? memo, CancellationToken ct)
    {
        var envelope = new { usage = new { quantity, memo } };
        var (status, body) = await Send(HttpMethod.Post, $"/subscriptions/{id}/components/{s.MeteredComponentId}/usages.json", envelope, ct);
        EnsureOk(status, body);
        using var doc = JsonDocument.Parse(body);
        var u = doc.RootElement.GetProperty("usage");
        return new UsageDto(GetLong(u, "id"), GetDouble(u, "quantity"));
    }

    private async Task<SubscriptionDto> OneSub(HttpMethod m, string path, object? env, CancellationToken ct)
    {
        var (status, body) = await Send(m, path, env, ct);
        EnsureOk(status, body);
        using var doc = JsonDocument.Parse(body);
        return MapSub(doc.RootElement.GetProperty("subscription"));
    }

    private static SubscriptionDto MapSub(JsonElement sub)
    {
        string? planHandle = sub.TryGetProperty("product", out var prod) && prod.ValueKind == JsonValueKind.Object
            ? GetStr(prod, "handle") : null;
        return new SubscriptionDto(GetLong(sub, "id"), GetStr(sub, "state") ?? "", planHandle);
    }

    private void EnsureOk(HttpStatusCode status, string body)
    {
        if ((int)status is >= 200 and < 300) return;
        if (status == HttpStatusCode.NotFound) throw new NotFoundException("The requested billing resource was not found.");
        if ((int)status is >= 400 and < 500)
            throw new ProviderRejectedException(breaks.NaiveErrors ? NaiveArrayOnly(body) : ParseErrors(body));
        throw new ProviderUnavailableException("The billing provider returned an error.");
    }

    // BREAK=naiveerrors: a lazy parser assuming ONLY Maxio's {"errors":[...]} array shape. It throws on the
    // field-map / single-string / non-JSON shapes -> surfaces as a 500 -> the new error-shape gate checks catch it.
    private static string NaiveArrayOnly(string body)
    {
        using var doc = JsonDocument.Parse(body);
        return string.Join("; ", doc.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()));
    }

    private static string ParseErrors(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("errors", out var errs))
                {
                    if (errs.ValueKind == JsonValueKind.Array)
                        return string.Join("; ", errs.EnumerateArray().Select(e => e.ToString()));
                    if (errs.ValueKind == JsonValueKind.Object)
                        return string.Join("; ", errs.EnumerateObject().Select(p => $"{p.Name}: {p.Value}"));
                }
                if (root.TryGetProperty("error", out var err)) return err.ToString();
            }
        }
        catch { /* not JSON — fall through to generic */ }
        return "The billing request was rejected.";
    }

    private async Task<(HttpStatusCode Status, string Body)> Send(HttpMethod method, string path, object? envelope, CancellationToken ct)
    {
        var idempotent = method == HttpMethod.Get;
        var maxAttempts = (idempotent || breaks.RetryWrite) ? 4 : 1;   // R5: writes are NOT retried
        Exception? last = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var req = new HttpRequestMessage(method, s.BaseUrl.TrimEnd('/') + path);
            if (!breaks.NoAuth)
                req.Headers.Authorization = new("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{s.ApiKey}:x")));
            if (envelope is not null)
                req.Content = new StringContent(JsonSerializer.Serialize(envelope, JsonOut), Encoding.UTF8, "application/json");

            var timeoutCts = breaks.NoTimeout ? null : new CancellationTokenSource(TimeSpan.FromSeconds(5)); // R4
            using var linked = timeoutCts is null
                ? CancellationTokenSource.CreateLinkedTokenSource(ct)
                : CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            try
            {
                using var resp = await http.SendAsync(req, linked.Token);
                var body = await resp.Content.ReadAsStringAsync(ct);
                var code = (int)resp.StatusCode;
                if ((code >= 500 || code == 429) && (idempotent || breaks.RetryWrite) && attempt < maxAttempts) { await Backoff(attempt, ct); continue; }
                return (resp.StatusCode, body);
            }
            catch (Exception ex) when ((ex is HttpRequestException || ex is OperationCanceledException) && !ct.IsCancellationRequested)
            {
                last = ex;                                              // transport error or per-attempt timeout
                if ((idempotent || breaks.RetryWrite) && attempt < maxAttempts) { await Backoff(attempt, ct); continue; }
                if (breaks.Raw500) throw;                               // BREAK: leak transport error as a raw 500
                throw new ProviderUnavailableException("The billing provider is currently unavailable.", ex); // R3/R4
            }
            finally { timeoutCts?.Dispose(); }
        }
        if (breaks.Raw500 && last is not null) throw last;
        throw new ProviderUnavailableException("The billing provider is currently unavailable.", last);
    }

    private static Task Backoff(int attempt, CancellationToken ct) => Task.Delay(TimeSpan.FromMilliseconds(40 * attempt), ct);

    private static long GetLong(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
    private static int GetInt(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
    private static double GetDouble(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return 0;
        return v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.TryParse(v.GetString(), out var d) ? d : 0;
    }
    private static string? GetStr(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
