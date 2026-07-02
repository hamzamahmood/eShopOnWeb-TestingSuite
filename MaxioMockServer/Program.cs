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

// Helpers for spec-shaped bodies.
// Products endpoint returns a bare JSON string on 404 (per the OpenAPI spec).
static IResult ProductFamilyNotFound() =>
    Results.Json("A valid product_family_id is required", statusCode: StatusCodes.Status404NotFound);

// Customer/subscription errors use the Error-List-Response shape: { "errors": [ ... ] }.
static IResult Errors(int statusCode, params string[] messages) =>
    Results.Json(new { errors = messages }, statusCode: statusCode);

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

// Anything else -> 404 with an errors body. Use an explicit catch-all pattern
// ("{**path}") rather than the parameterless MapFallback, whose default
// "{*path:nonfile}" constraint would skip file-like paths such as
// "/customers/abc/subscriptions.json" (non-integer id) and leave them with an
// empty body.
app.MapFallback("/{**path}",
    () => Errors(StatusCodes.Status404NotFound, "The requested resource was not found."));

app.Logger.LogInformation("Maxio mock server listening on http://localhost:8080");
app.Run();
