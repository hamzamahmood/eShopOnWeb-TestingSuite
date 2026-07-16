using System.Net;
using System.Text;
using System.Text.Json;

namespace ReferencePetstore;

public sealed class PetstoreSettings
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string OrgId { get; set; } = "";
}

/// <summary>Deliberate-defect toggles (env BREAK=...), used to prove the gate discriminates on this API.</summary>
public sealed record Breaks(bool Leak, bool RetryWrite, bool NoTimeout, bool Raw500, bool NoAuth, bool LogSecret, bool Hardcode, bool ShallowMap)
{
    public static Breaks From(string? env)
    {
        var s = (env ?? "").ToLowerInvariant();
        bool H(string k) => s.Contains(k);
        return new Breaks(H("leak"), H("retrywrite"), H("notimeout"), H("raw500"), H("noauth"), H("logsecret"), H("hardcode"), H("shallowmap"));
    }
}

public sealed class NotFoundException(string message) : Exception(message);
public sealed class ValidationFailedException(IReadOnlyList<string> errors)
    : Exception(string.Join("; ", errors)) { public IReadOnlyList<string> Errors { get; } = errors; }
public sealed class ProviderRejectedException(string message) : Exception(message);
public sealed class ProviderUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public sealed record PetDto(long Id, string Name, string Status, string? Category);
public sealed record OrderDto(long Id, long PetId, long Quantity, string Status);
public sealed record UserDto(long Id, string Username, string FirstName, string LastName, string Email);

/// <summary>
/// A correct raw-HttpClient Swagger Petstore client: hand-rolled resilience (retry idempotent GETs only —
/// NEVER writes — with a per-attempt timeout), transport/timeout errors wrapped into domain exceptions,
/// api_key-header auth, and Petstore { code, type, message } error-shape parsing. This is the hand-rolled
/// counterpart to what an SDK would provide "for free".
/// </summary>
public sealed class PetstoreClient(HttpClient http, PetstoreSettings s, Breaks breaks)
{
    private static readonly JsonSerializerOptions JsonOut = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<PetDto>> FindByStatusAsync(string status, CancellationToken ct)
    {
        var (code, body) = await Send(HttpMethod.Get, $"/pet/findByStatus?status={Uri.EscapeDataString(status)}", null, ct);
        EnsureOk(code, body);
        return MapPets(body);
    }

    public async Task<IReadOnlyList<PetDto>> FindByTagsAsync(string tags, CancellationToken ct)
    {
        var (code, body) = await Send(HttpMethod.Get, $"/pet/findByTags?tags={Uri.EscapeDataString(tags)}", null, ct);
        EnsureOk(code, body);
        return MapPets(body);
    }

    public async Task<PetDto> GetPetAsync(long petId, CancellationToken ct)
    {
        var (code, body) = await Send(HttpMethod.Get, $"/pet/{petId}", null, ct);
        EnsureOk(code, body);
        using var doc = JsonDocument.Parse(body);
        return MapPet(doc.RootElement);
    }

    public async Task<PetDto> CreatePetAsync(string name, IReadOnlyList<string> photoUrls, string? status, CancellationToken ct)
    {
        var payload = new { name, photoUrls, status };
        var (code, body) = await Send(HttpMethod.Post, "/pet", payload, ct);
        EnsureOk(code, body);
        using var doc = JsonDocument.Parse(body);
        return MapPet(doc.RootElement);
    }

    public async Task<OrderDto> CreateOrderAsync(long petId, long quantity, string? status, CancellationToken ct)
    {
        var payload = new { petId, quantity, status };
        var (code, body) = await Send(HttpMethod.Post, "/store/order", payload, ct);
        EnsureOk(code, body);
        using var doc = JsonDocument.Parse(body);
        return MapOrder(doc.RootElement);
    }

    public async Task<OrderDto> GetOrderAsync(long orderId, CancellationToken ct)
    {
        var (code, body) = await Send(HttpMethod.Get, $"/store/order/{orderId}", null, ct);
        EnsureOk(code, body);
        using var doc = JsonDocument.Parse(body);
        return MapOrder(doc.RootElement);
    }

    public async Task<UserDto> GetUserAsync(string username, CancellationToken ct)
    {
        var (code, body) = await Send(HttpMethod.Get, $"/user/{Uri.EscapeDataString(username)}", null, ct);
        EnsureOk(code, body);
        using var doc = JsonDocument.Parse(body);
        var u = doc.RootElement;
        return new UserDto(GetLong(u, "id"), GetStr(u, "username") ?? "", GetStr(u, "firstName") ?? "",
            GetStr(u, "lastName") ?? "", GetStr(u, "email") ?? "");
    }

    private static IReadOnlyList<PetDto> MapPets(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var list = new List<PetDto>();
        foreach (var item in doc.RootElement.EnumerateArray()) list.Add(MapPet(item));
        return list;
    }

    private static PetDto MapPet(JsonElement p)
    {
        string? category = p.TryGetProperty("category", out var cat) && cat.ValueKind == JsonValueKind.Object
            ? GetStr(cat, "name") : null;
        return new PetDto(GetLong(p, "id"), GetStr(p, "name") ?? "", GetStr(p, "status") ?? "", category);
    }

    private static OrderDto MapOrder(JsonElement o)
        => new(GetLong(o, "id"), GetLong(o, "petId"), GetLong(o, "quantity"), GetStr(o, "status") ?? "");

    private static void EnsureOk(HttpStatusCode status, string body)
    {
        if ((int)status is >= 200 and < 300) return;
        if (status == HttpStatusCode.NotFound) throw new NotFoundException("The requested store resource was not found.");
        if ((int)status is >= 400 and < 500) throw new ProviderRejectedException(ParseError(body));
        throw new ProviderUnavailableException("The store provider returned an error.");
    }

    // Petstore errors are { code, type, message }; fall back to a generic message if the shape differs.
    private static string ParseError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String) return m.GetString()!;
                if (root.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array)
                    return string.Join("; ", errs.EnumerateArray().Select(e => e.ToString()));
            }
        }
        catch { /* not JSON — fall through */ }
        return "The store request was rejected.";
    }

    private async Task<(HttpStatusCode Status, string Body)> Send(HttpMethod method, string path, object? payload, CancellationToken ct)
    {
        var idempotent = method == HttpMethod.Get;
        var maxAttempts = (idempotent || breaks.RetryWrite) ? 4 : 1;   // R5: writes are NOT retried
        Exception? last = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var req = new HttpRequestMessage(method, s.BaseUrl.TrimEnd('/') + path);
            if (!breaks.NoAuth)
            {
                req.Headers.TryAddWithoutValidation("api_key", s.ApiKey);
                if (!string.IsNullOrEmpty(s.OrgId)) req.Headers.TryAddWithoutValidation("x-org-id", s.OrgId);
            }
            if (payload is not null)
                req.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOut), Encoding.UTF8, "application/json");

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
                throw new ProviderUnavailableException("The store provider is currently unavailable.", ex); // R3/R4
            }
            finally { timeoutCts?.Dispose(); }
        }
        if (breaks.Raw500 && last is not null) throw last;
        throw new ProviderUnavailableException("The store provider is currently unavailable.", last);
    }

    private static Task Backoff(int attempt, CancellationToken ct) => Task.Delay(TimeSpan.FromMilliseconds(40 * attempt), ct);

    private static long GetLong(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
    private static string? GetStr(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
