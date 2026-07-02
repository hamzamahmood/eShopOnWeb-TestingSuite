using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MaxioMockServer.Middleware;

/// <summary>
/// Validates incoming request bodies against the Maxio OpenAPI (openAPI/openapi.yaml) contract for the
/// mutating endpoints, BEFORE the endpoint's own business logic runs. This makes the mock a higher-fidelity
/// simulator: a request that violates the spec is rejected with a spec-shaped <c>{"errors":[...]}</c> 422,
/// exactly as the real Maxio API would, rather than being silently accepted.
///
/// <para>
/// Scope is deliberately spec-faithful, not maximal:
/// <list type="bullet">
///   <item>the wrapper key each create/update requires (<c>customer</c>/<c>subscription</c>/<c>usage</c>/<c>migration</c>) must be present and be an object;</item>
///   <item>fields the spec marks <c>required</c> (only <c>createCustomer</c> has any: first_name, last_name, email) must be present and non-null;</item>
///   <item>when present, a field must match its declared JSON type, and <c>payment_collection_method</c> must be one of the spec's enum values;</item>
///   <item>unknown/extra properties are NOT rejected — the spec sets no <c>additionalProperties: false</c>, so a faithful mock tolerates them (e.g. the Plugin sends a <c>cancel_at_end_of_period</c> on cancel that the current spec's Cancellation-Options schema does not list).</item>
/// </list>
/// Registered AFTER the logging middleware so rejections are still logged, and it rewinds the buffered body
/// so the endpoint's model binding can re-read it.
/// </para>
/// </summary>
public sealed class StrictValidationMiddleware
{
    private readonly RequestDelegate _next;

    public StrictValidationMiddleware(RequestDelegate next) => _next = next;

    /// <summary>Collection-Method.yaml enum (Create-Subscription.payment_collection_method).</summary>
    private static readonly string[] CollectionMethods = { "automatic", "remittance", "prepaid", "invoice" };

    public async Task InvokeAsync(HttpContext context)
    {
        var validate = Match(context.Request.Method, context.Request.Path.Value ?? string.Empty);
        if (validate is null)
        {
            await _next(context);
            return;
        }

        context.Request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
        }
        context.Request.Body.Position = 0;

        var errors = validate(body);
        if (errors.Count > 0)
        {
            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { errors }));
            return;
        }

        await _next(context);
    }

    private static Func<string, List<string>>? Match(string method, string path) => (method, path) switch
    {
        ("POST", "/customers.json") => ValidateCreateCustomer,
        ("POST", "/subscriptions.json") => ValidateCreateSubscription,
        ("POST", _) when Regex.IsMatch(path, @"^/subscriptions/[^/]+/components/[^/]+/usages\.json$") => ValidateCreateUsage,
        ("POST", _) when Regex.IsMatch(path, @"^/subscriptions/[^/]+/migrations\.json$") => ValidateMigration,
        ("DELETE", _) when Regex.IsMatch(path, @"^/subscriptions/[^/]+\.json$") => ValidateCancel,
        _ => null
    };

    // POST /customers.json — Create-Customer requires [first_name, last_name, email].
    private static List<string> ValidateCreateCustomer(string body) =>
        ValidateEnvelope(body, "customer", bodyRequired: true, (c, e) =>
        {
            Require(c, "first_name", "First name", JsonValueKind.String, e);
            Require(c, "last_name", "Last name", JsonValueKind.String, e);
            Require(c, "email", "Email", JsonValueKind.String, e);
            Optional(c, "reference", "Reference", JsonValueKind.String, e);
        });

    // POST /subscriptions.json — Create-Subscription has no required attributes; validate types + enum only.
    private static List<string> ValidateCreateSubscription(string body) =>
        ValidateEnvelope(body, "subscription", bodyRequired: true, (s, e) =>
        {
            OptionalNumber(s, "customer_id", "Customer ID", e);
            OptionalNumber(s, "product_id", "Product ID", e);
            Optional(s, "product_handle", "Product handle", JsonValueKind.String, e);
            Optional(s, "customer_reference", "Customer reference", JsonValueKind.String, e);
            OptionalEnum(s, "payment_collection_method", "Payment collection method", CollectionMethods, e);
        });

    // POST /subscriptions/{id}/components/{comp}/usages.json — Create-Usage (quantity is number, memo string).
    private static List<string> ValidateCreateUsage(string body) =>
        ValidateEnvelope(body, "usage", bodyRequired: true, (u, e) =>
        {
            OptionalNumber(u, "quantity", "Quantity", e);
            Optional(u, "memo", "Memo", JsonValueKind.String, e);
        });

    // POST /subscriptions/{id}/migrations.json — Subscription-Product-Migration.
    private static List<string> ValidateMigration(string body) =>
        ValidateEnvelope(body, "migration", bodyRequired: true, (m, e) =>
        {
            OptionalNumber(m, "product_id", "Product ID", e);
            Optional(m, "product_handle", "Product handle", JsonValueKind.String, e);
            OptionalBool(m, "preserve_period", "Preserve period", e);
        });

    // DELETE /subscriptions/{id}.json — Cancellation-Options. Body is optional; validate only if present.
    private static List<string> ValidateCancel(string body) =>
        ValidateEnvelope(body, "subscription", bodyRequired: false, (s, e) =>
        {
            Optional(s, "cancellation_message", "Cancellation message", JsonValueKind.String, e);
            Optional(s, "reason_code", "Reason code", JsonValueKind.String, e);
        });

    private static List<string> ValidateEnvelope(string body, string envelope, bool bodyRequired, Action<JsonObject, List<string>> validateAttributes)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(body))
        {
            if (bodyRequired)
            {
                errors.Add($"{Label(envelope)}: must be provided.");
            }
            return errors;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            errors.Add("Request body must be valid JSON.");
            return errors;
        }

        if (root is not JsonObject obj)
        {
            errors.Add("Request body must be a JSON object.");
            return errors;
        }

        if (!Present(obj, envelope))
        {
            errors.Add($"{Label(envelope)}: must be provided.");
            return errors;
        }

        if (obj[envelope] is not JsonObject attributes)
        {
            errors.Add($"{Label(envelope)}: must be an object.");
            return errors;
        }

        validateAttributes(attributes, errors);
        return errors;
    }

    /// <summary>A property is "present" only when the key exists AND its value is not JSON null.</summary>
    private static bool Present(JsonObject o, string name) => o.ContainsKey(name) && o[name] is not null;

    private static void Require(JsonObject o, string name, string label, JsonValueKind kind, List<string> errors)
    {
        if (!Present(o, name))
        {
            errors.Add($"{label}: cannot be blank.");
            return;
        }
        if (o[name]!.GetValueKind() != kind)
        {
            errors.Add($"{label}: is invalid.");
        }
    }

    private static void Optional(JsonObject o, string name, string label, JsonValueKind kind, List<string> errors)
    {
        if (Present(o, name) && o[name]!.GetValueKind() != kind)
        {
            errors.Add($"{label}: is invalid.");
        }
    }

    // OpenAPI `type: integer` and `type: number` both map to a JSON number; the mock validates at that granularity.
    private static void OptionalNumber(JsonObject o, string name, string label, List<string> errors)
    {
        if (Present(o, name) && o[name]!.GetValueKind() != JsonValueKind.Number)
        {
            errors.Add($"{label}: must be a number.");
        }
    }

    private static void OptionalBool(JsonObject o, string name, string label, List<string> errors)
    {
        if (!Present(o, name))
        {
            return;
        }
        var kind = o[name]!.GetValueKind();
        if (kind != JsonValueKind.True && kind != JsonValueKind.False)
        {
            errors.Add($"{label}: must be a boolean.");
        }
    }

    private static void OptionalEnum(JsonObject o, string name, string label, string[] allowed, List<string> errors)
    {
        if (!Present(o, name))
        {
            return;
        }
        if (o[name]!.GetValueKind() != JsonValueKind.String || !allowed.Contains(o[name]!.GetValue<string>()))
        {
            errors.Add($"{label}: must be one of: {string.Join(", ", allowed)}.");
        }
    }

    private static string Label(string envelope) =>
        char.ToUpperInvariant(envelope[0]) + envelope[1..];
}
