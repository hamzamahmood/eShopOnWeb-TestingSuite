# Maxio Advanced Billing — Mock Server

A tiny ASP.NET Core (.NET 10, Minimal API) mock of the **Maxio Advanced Billing (Chargify)** API,
for local development/testing of the eShop integration. It serves fixed, spec-shaped responses for
three read endpoints and logs every request/response.

Base URL: **`http://localhost:8080`**

## Endpoints

| Purpose | Route |
|---------|-------|
| List available plans (products) | `GET /product_families/{product_family_id}/products.json` |
| Look up customer by reference | `GET /customers/lookup.json?reference=...` |
| List a customer's subscriptions | `GET /customers/{customer_id}/subscriptions.json` |

Responses match the shapes in `../openapi.yaml` (`Product-Response`, `Customer-Response`,
`Subscription-Response`, and the `{ "errors": [...] }` error shape).

## Known (canned) identifiers

Responses are **static**. To exercise both success and error paths, the server recognizes a small
set of known ids — matching ids return the canned payload, everything else returns a `404`:

| Endpoint | Known value(s) → 200 | Anything else → 404 |
|----------|----------------------|---------------------|
| products | `527890`, `handle:acme-projects` | `"A valid product_family_id is required"` |
| customer lookup | `reference=cust_12345` | `{ "errors": ["Customer not found."] }` |
| subscriptions | `customer_id=98765` | `{ "errors": ["Customer not found."] }` |

The canned data is internally consistent, so a realistic flow works end-to-end:
lookup `cust_12345` → customer `id: 98765` → list subscriptions for `98765`.

You can add/edit ids in `MockStore.cs` and payloads in `MockData/*.json`.

## Run

```sh
cd MaxioMockServer
dotnet run
```

The server listens on `http://localhost:8080` (see the startup log line).

## Try it

Success paths:

```sh
curl http://localhost:8080/product_families/527890/products.json
curl "http://localhost:8080/customers/lookup.json?reference=cust_12345"
curl http://localhost:8080/customers/98765/subscriptions.json
```

Error paths (add `-i` to see the status code):

```sh
curl -i http://localhost:8080/product_families/999/products.json         # 404 bare JSON string
curl -i "http://localhost:8080/customers/lookup.json?reference=nope"     # 404 errors body
curl -i "http://localhost:8080/customers/lookup.json"                    # 404 missing reference
curl -i http://localhost:8080/customers/111111/subscriptions.json        # 404 errors body
curl -i http://localhost:8080/customers/abc/subscriptions.json           # 404 (non-integer id)
curl -i http://localhost:8080/unknown/route                              # 404 fallback
```

## Logging

Every request and response is logged to the **console** and appended to
`logs/requests-YYYY-MM-DD.log`. Response bodies are truncated in the log at 2,000 characters
(the full body is still returned to the client). The `logs/` folder is created automatically.

## Authentication

The real Maxio API uses HTTP Basic auth (`username` = API key, `password` = `x`). **This mock does
not enforce authentication** — all requests are accepted regardless of the `Authorization` header.

## Notes / limitations

- Query parameters (`page`, `per_page`, `filter[...]`, date filters, `include`) are accepted but
  **not applied** — the static payloads are returned as-is.
- Only the three routes above are implemented; any other path/method returns a `404` fallback.
