using System.Text.Json.Serialization;
using MaxioMockServer;
using MaxioMockServer.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Bind Kestrel to localhost:8080 as required.
builder.WebHost.UseUrls("http://localhost:8080");

// Load the canned mock payloads once at startup.
var store = MockStore.Load(builder.Environment.ContentRootPath);
builder.Services.AddSingleton(store);

var app = builder.Build();

// Log every incoming request and its response (console + logs/ file).
app.UseMiddleware<RequestResponseLoggingMiddleware>();

// Reject requests whose bodies violate the Maxio OpenAPI contract (required/type/enum), spec-shaped 422.
// Runs after logging (so rejections are logged) and before the route handlers.
app.UseMiddleware<StrictValidationMiddleware>();

// Helpers for spec-shaped bodies.
// Products endpoint returns a bare JSON string on 404 (per the OpenAPI spec).
static IResult ProductFamilyNotFound() =>
    Results.Json("A valid product_family_id is required", statusCode: StatusCodes.Status404NotFound);

// Customer/subscription/component errors use the Error-List-Response shape: { "errors": [ ... ] }.
static IResult Errors(int statusCode, params string[] messages) =>
    Results.Json(new { errors = messages }, statusCode: statusCode);

// Every subscription-scoped route (read/pause/resume/reactivate/migrate/cancel/usage) 404s the same way
// for an id outside MockStore.SubscriptionsById.
static IResult SubscriptionNotFound() => Errors(StatusCodes.Status404NotFound, "Subscription not found.");

// 1. List available plans -----------------------------------------------------
//    GET /product_families/{product_family_id}/products.json
app.MapGet("/product_families/{product_family_id}/products.json",
    (string product_family_id, MockStore mocks) =>
        mocks.KnownProductFamilyIds.Contains(product_family_id)
            ? Results.Text(mocks.ProductsJson, "application/json")
            : ProductFamilyNotFound());

// 2. Look up customer by reference --------------------------------------------
//    GET /customers/lookup.json?reference=...
app.MapGet("/customers/lookup.json",
    (HttpContext ctx, MockStore mocks) =>
    {
        var reference = ctx.Request.Query["reference"].ToString();

        if (string.IsNullOrWhiteSpace(reference))
        {
            return Errors(StatusCodes.Status404NotFound, "A customer reference is required.");
        }

        // --- Comparison-harness transient-failure behaviors (keyed on the reference) ------------------
        // The FIRST attempt for a given reference fails transiently (503/429); a RETRIED attempt succeeds.
        // NOTE: this is NOT a Direct-vs-Plugin differentiator - BOTH integrations retry idempotent GETs
        // (Direct via a Microsoft.Extensions.Http.Resilience/Polly pipeline in MaxioDependencies.cs,
        // Plugin via the SDK's default RetryOptions), so both recover from this transient failure on the
        // customer-lookup GET. Retained as a mock capability only. Keyed per-reference so a unique nonce
        // per test run keeps it independent of test ordering. Gated behind dedicated prefixes so the
        // existing customer route/tests are unaffected.
        if (reference.StartsWith("retry_", StringComparison.Ordinal))
        {
            // Generic transient 5xx (e.g. a brief upstream outage).
            return mocks.NextAttempt(reference) == 1
                ? Errors(StatusCodes.Status503ServiceUnavailable,
                    "The billing service is temporarily unavailable. Please retry.")
                : Results.Text(mocks.CustomerJson, "application/json");
        }

        if (reference.StartsWith("ratelimit_", StringComparison.Ordinal))
        {
            // Maxio-documented 429 rate limit; real Maxio also sends Retry-After.
            if (mocks.NextAttempt(reference) == 1)
            {
                ctx.Response.Headers.RetryAfter = "1";
                return Errors(StatusCodes.Status429TooManyRequests,
                    "Too many requests for this customer. You can perform 5 requests within 00:30:00.");
            }

            return Results.Text(mocks.CustomerJson, "application/json");
        }

        // --- Concurrent-create race demonstration (see ../MaxioPassthroughApiTests PluginAdvantageTests) ---
        // The FIRST lookup for a race_ reference misses, so the caller proceeds to create. The create loses to
        // a "concurrent" create (POST /customers.json below returns 422), after which the customer genuinely
        // exists - so this re-lookup (which only the Plugin performs, after catching the create conflict)
        // finds it. Keyed per-reference via NextAttempt so a fresh nonce per run stays order-independent.
        if (reference.StartsWith("race_", StringComparison.Ordinal))
        {
            return mocks.NextAttempt(reference) == 1
                ? Errors(StatusCodes.Status404NotFound, "Customer not found.")
                : Results.Text(
                    MockStore.NewCustomerJson(98767, reference, "race.recovered@example.com", "Race", "Recovered"),
                    "application/json");
        }

        return mocks.KnownCustomerReferences.Contains(reference)
            ? Results.Text(mocks.CustomerJson, "application/json")
            : Errors(StatusCodes.Status404NotFound, "Customer not found.");
    });

// 3. List a customer's subscriptions ------------------------------------------
//    GET /customers/{customer_id}/subscriptions.json
//    The :int constraint means a non-integer id never matches and falls through
//    to the fallback 404 below.
app.MapGet("/customers/{customer_id:int}/subscriptions.json",
    (int customer_id, MockStore mocks) =>
        mocks.KnownCustomerIds.Contains(customer_id)
            ? Results.Text(mocks.SubscriptionsJson, "application/json")
            : Errors(StatusCodes.Status404NotFound, "Customer not found."));

// 4. Create (or reject a duplicate) customer -----------------------------------
//    POST /customers.json
//    The mock is stateless: any reference NOT already in KnownCustomerReferences always "creates"
//    successfully with the same fixed, deterministic id (98766) - repeat calls for the same fresh
//    reference are therefore idempotent, matching Maxio's real per-reference uniqueness guarantee that
//    FindOrCreateCustomerAsync relies on (see ../MaxioPassthroughApiTests CLAUDE.md).
app.MapPost("/customers.json",
    (CustomerEnvelope body, MockStore mocks) =>
    {
        var reference = body.Customer?.Reference;
        var email = body.Customer?.Email;

        if (string.IsNullOrWhiteSpace(email))
        {
            return Errors(StatusCodes.Status422UnprocessableEntity, "Email address: cannot be blank.");
        }

        // A "concurrent" request created this reference between the caller's lookup (which missed, see the
        // race_ branch of the lookup route) and this create - Maxio rejects the duplicate. The Plugin recovers
        // by re-reading; the Direct client surfaces the conflict. See PluginAdvantageTests.
        if (!string.IsNullOrWhiteSpace(reference) && reference.StartsWith("race_", StringComparison.Ordinal))
        {
            return Errors(StatusCodes.Status422UnprocessableEntity, "Reference has already been taken");
        }

        if (!string.IsNullOrWhiteSpace(reference) && mocks.KnownCustomerReferences.Contains(reference))
        {
            return Errors(StatusCodes.Status422UnprocessableEntity, "Reference has already been taken");
        }

        return Results.Text(
            MockStore.NewCustomerJson(98766, reference ?? string.Empty, email, body.Customer?.FirstName, body.Customer?.LastName),
            "application/json");
    });

// 5. Read a subscription --------------------------------------------------------
//    GET /subscriptions/{subscription_id}.json
app.MapGet("/subscriptions/{subscription_id:int}.json",
    (int subscription_id, MockStore mocks) =>
        mocks.SubscriptionsById.TryGetValue(subscription_id, out var json)
            ? Results.Text(json, "application/json")
            : SubscriptionNotFound());

// 6. Create a subscription -------------------------------------------------------
//    POST /subscriptions.json
//    Known customer (98765) + known product handle -> 201 Created. Unknown customer or product handle
//    -> 422 (matches Maxio's validation-style errors, not a 404 - the resource being created doesn't exist yet).
app.MapPost("/subscriptions.json",
    (SubscriptionEnvelope body, MockStore mocks) =>
    {
        var customerId = body.Subscription?.CustomerId;
        var productHandle = body.Subscription?.ProductHandle;

        if (customerId is null || !mocks.KnownCustomerIds.Contains(customerId.Value))
        {
            return Errors(StatusCodes.Status422UnprocessableEntity, "Customer: must exist");
        }

        // A product whose activation requires a verified payment method the caller didn't supply. Maxio
        // returns a 422 carrying card/payment validation messages. The Plugin classifies these (keyword match)
        // as a typed PaymentVerificationRequiredException with a user-actionable message; the Direct client
        // surfaces the raw messages generically. See ../MaxioPassthroughApiTests PluginAdvantageTests.
        if (string.Equals(productHandle, "card-required", StringComparison.Ordinal))
        {
            return Errors(StatusCodes.Status422UnprocessableEntity,
                "Payment method is required to activate this subscription.",
                "The credit card on file could not be verified.");
        }

        if (string.IsNullOrWhiteSpace(productHandle) || !mocks.KnownProductHandles.Contains(productHandle))
        {
            return Errors(StatusCodes.Status422UnprocessableEntity, "Product doesn't exist");
        }

        return Results.Text(
            mocks.NewSubscriptionJson(15100300, customerId.Value, productHandle),
            "application/json",
            statusCode: StatusCodes.Status201Created);
    });

// 7. Pause (hold) a subscription --------------------------------------------------
//    POST /subscriptions/{subscription_id}/hold.json
//    Only the active canned subscription (15100121) is eligible to be held.
app.MapPost("/subscriptions/{subscription_id:int}/hold.json",
    (int subscription_id, MockStore mocks) =>
    {
        if (!mocks.SubscriptionsById.TryGetValue(subscription_id, out var json))
        {
            return SubscriptionNotFound();
        }

        return subscription_id == 15100121
            ? Results.Text(MockStore.WithState(json, "on_hold"), "application/json")
            : Errors(StatusCodes.Status422UnprocessableEntity, "This subscription is not eligible to be put on hold.");
    });

// 8. Resume a subscription ---------------------------------------------------------
//    POST /subscriptions/{subscription_id}/resume.json
//    Only the on-hold canned subscription (15100210) can be resumed.
app.MapPost("/subscriptions/{subscription_id:int}/resume.json",
    (int subscription_id, MockStore mocks) =>
    {
        if (!mocks.SubscriptionsById.TryGetValue(subscription_id, out var json))
        {
            return SubscriptionNotFound();
        }

        return subscription_id == 15100210
            ? Results.Text(MockStore.WithState(json, "active"), "application/json")
            : Errors(StatusCodes.Status422UnprocessableEntity, "Only subscriptions that are on hold can be resumed.");
    });

// 9. Reactivate a subscription -------------------------------------------------------
//    PUT /subscriptions/{subscription_id}/reactivate.json
//    Only the canceled canned subscription (15100299) can be reactivated.
app.MapPut("/subscriptions/{subscription_id:int}/reactivate.json",
    (int subscription_id, MockStore mocks) =>
    {
        if (!mocks.SubscriptionsById.TryGetValue(subscription_id, out var json))
        {
            return SubscriptionNotFound();
        }

        return subscription_id == 15100299
            ? Results.Text(MockStore.WithState(json, "active"), "application/json")
            : Errors(StatusCodes.Status422UnprocessableEntity,
                "Cannot reactivate a subscription that is not marked \"Canceled\", \"Unpaid\", or \"Trial Ended\".");
    });

// 10. Migrate a subscription's product --------------------------------------------------
//    POST /subscriptions/{subscription_id}/migrations.json
//    Only the active canned subscription (15100121) is eligible; the target product handle must be known.
app.MapPost("/subscriptions/{subscription_id:int}/migrations.json",
    (int subscription_id, MigrationEnvelope body, MockStore mocks) =>
    {
        if (!mocks.SubscriptionsById.TryGetValue(subscription_id, out var json))
        {
            return SubscriptionNotFound();
        }

        if (subscription_id != 15100121)
        {
            return Errors(StatusCodes.Status422UnprocessableEntity, "Subscription must be active");
        }

        var productHandle = body.Migration?.ProductHandle;
        if (string.IsNullOrWhiteSpace(productHandle) || !mocks.KnownProductHandles.Contains(productHandle))
        {
            return Errors(StatusCodes.Status422UnprocessableEntity, "Invalid Product");
        }

        return Results.Text(mocks.WithProduct(json, productHandle), "application/json");
    });

// 11. Cancel a subscription -----------------------------------------------------------
//    DELETE /subscriptions/{subscription_id}.json
//    The already-canceled canned subscription (15100299) reproduces Maxio's documented
//    Cancel-Subscription-Error-Response, which (unlike every other error here) uses the SINGULAR
//    "error" key, not "errors".
app.MapDelete("/subscriptions/{subscription_id:int}.json",
    (int subscription_id, MockStore mocks) =>
    {
        if (!mocks.SubscriptionsById.TryGetValue(subscription_id, out var json))
        {
            return SubscriptionNotFound();
        }

        if (subscription_id == 15100299)
        {
            return Results.Json(new { error = "The subscription is already canceled" }, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.Text(MockStore.WithState(json, "canceled", canceledAt: "2026-07-02T12:00:00-04:00"), "application/json");
    });

// 12. Record usage ----------------------------------------------------------------------
//    POST /subscriptions/{subscription_id}/components/{component_id}/usages.json
//    component_id is accepted but not validated (both integrations always send the one configured
//    metered component); only the subscription id needs to be known.
app.MapPost("/subscriptions/{subscription_id:int}/components/{component_id}/usages.json",
    (int subscription_id, string component_id, UsageEnvelope body, MockStore mocks) =>
        mocks.SubscriptionsById.ContainsKey(subscription_id)
            ? Results.Text(MockStore.NewUsageJson(138522957, subscription_id, body.Usage?.Quantity ?? 0m, body.Usage?.Memo), "application/json")
            : SubscriptionNotFound());

// 13. Read the configured metered component ----------------------------------------------
//    GET /product_families/{product_family_id}/components/{component_id}.json
//    Prerequisite call the Direct client's RecordUsageAsync always makes before createUsage
//    (readComponent, to verify the configured component is metered) - see MaxioBillingClient.cs.
app.MapGet("/product_families/{product_family_id}/components/{component_id}.json",
    (string product_family_id, string component_id, MockStore mocks) =>
        mocks.KnownProductFamilyIds.Contains(product_family_id) && mocks.KnownComponentTokens.Contains(component_id)
            ? Results.Text(mocks.ComponentJson, "application/json")
            : Errors(StatusCodes.Status404NotFound, "Component not found."));

// Anything else -> 404 with an errors body. Use an explicit catch-all pattern
// ("{**path}") rather than the parameterless MapFallback, whose default
// "{*path:nonfile}" constraint would skip file-like paths such as
// "/customers/abc/subscriptions.json" (non-integer id) and leave them with an
// empty body.
app.MapFallback("/{**path}",
    () => Errors(StatusCodes.Status404NotFound, "The requested resource was not found."));

app.Logger.LogInformation("Maxio mock server listening on http://localhost:8080");
app.Run();

// Request-body shapes (snake_case wire fields per openAPI/) for the mutating routes above. Declared as
// top-level types at the end of the file per C#'s top-level-statements rule (they must follow all
// top-level statements).
internal sealed record CustomerEnvelope([property: JsonPropertyName("customer")] CustomerAttributes? Customer);

internal sealed record CustomerAttributes(
    [property: JsonPropertyName("reference")] string? Reference,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName);

internal sealed record SubscriptionEnvelope([property: JsonPropertyName("subscription")] SubscriptionAttributes? Subscription);

internal sealed record SubscriptionAttributes(
    [property: JsonPropertyName("customer_id")] int? CustomerId,
    [property: JsonPropertyName("product_handle")] string? ProductHandle);

internal sealed record MigrationEnvelope([property: JsonPropertyName("migration")] MigrationAttributes? Migration);

internal sealed record MigrationAttributes([property: JsonPropertyName("product_handle")] string? ProductHandle);

internal sealed record UsageEnvelope([property: JsonPropertyName("usage")] UsageAttributes? Usage);

internal sealed record UsageAttributes(
    [property: JsonPropertyName("quantity")] decimal? Quantity,
    [property: JsonPropertyName("memo")] string? Memo);
