# MaxioPassthroughApiTests

Black-box HTTP tests for the Maxio **passthrough** endpoints exposed by the eShopOnWeb PublicApi:

| Route | Maxio operation |
|---|---|
| `GET /api/listplans` | list products for the configured product family |
| `GET /api/customer?reference={ref}` | look up a customer by reference |
| `GET /api/subscription?customerId={id}` | list a customer's subscriptions |

This project is **standalone**: it lives outside both `eShopOnWeb-Direct.sln` and `eShopOnWeb-Plugin.sln`,
has **no project references**, and talks to the running PublicApi purely over HTTP (the `curl -k`
equivalent). The exact same tests validate **either** integration — just point `PUBLICAPI_BASEURL` at
whichever one is running. Because both integrations proxy Maxio's raw response and pass its exact status
through, the assertions (specific fields + exact 404) hold for both.

## Tests (5)

| File | Test | Expectation |
|---|---|---|
| `ListPlansTests` | success | `200` + both mock products; Gold Plan fields (`id 3858146`, `price_in_cents 1000`, `product_family.id 527890`) |
| `CustomerLookupTests` | success | `200` + customer object (`id 98765`, `reference cust_12345`) |
| `CustomerLookupTests` | failure | unknown reference → **`404`** + Maxio `{ "errors": ["Customer not found."] }` (proves exact-status passthrough, not the old `422`) |
| `SubscriptionTests` | success | `200` + subscription array (`customer.id 98765`, `product.handle "gold"`, `state "active"`) |
| `SubscriptionTests` | failure | unknown numeric customer id → **`404`** + Maxio `{ "errors": ["Customer not found."] }` |

> `/api/listplans` has no request input (it uses the PublicApi's configured product family), so only a
> success test is meaningful — a "wrong input parameter" case can't be triggered from the request.

## Prerequisites to run end-to-end

The tests call the PublicApi, which in turn calls Maxio. To exercise them against canned data you need
**three things running/wired**:

1. **The Maxio mock server** (canned Maxio responses on `http://localhost:8080`):
   ```sh
   cd ../openAPI/MaxioMockServer
   dotnet run
   ```
   Known data: product family `527890` / `handle:acme-projects`; customer reference `cust_12345` → id
   `98765`; that customer has one Gold Plan subscription.

2. **One eShopOnWeb PublicApi** (Direct *or* Plugin) running, **with its Maxio calls routed to the mock**.
   The integration repos were intentionally left unmodified, so **redirecting the app's Maxio base URL to
   `http://localhost:8080` is up to you** (e.g. a local reverse proxy / hosts mapping, or a temporary
   config/code tweak in your own checkout). The app also needs, under the `Maxio` config section:
   - `Maxio:ProductFamilyId=527890` **and** `Maxio:ProductFamilyHandle=acme-projects` (so `/api/listplans`
     resolves to the mock's known family — Plugin uses the id, Direct uses the handle),
   - any non-empty `Maxio:ApiKey` and `Maxio:Subdomain` (the mock enforces no auth),
   - `Maxio:SkipStartupValidation=true` (skip the startup metered-component check).

3. **This test project**, pointed at that PublicApi.

## Run

```sh
# default base URL is https://localhost:5099
dotnet test

# or target a specific instance / port
PUBLICAPI_BASEURL=https://localhost:5099 dotnet test
```

## Configuration (environment variables)

All optional; defaults match the mock's canned data.

| Variable | Default | Meaning |
|---|---|---|
| `PUBLICAPI_BASEURL` | `https://localhost:5099` | Base URL of the PublicApi under test |
| `KNOWN_CUSTOMER_REFERENCE` | `cust_12345` | A reference the mock resolves |
| `KNOWN_CUSTOMER_ID` | `98765` | A customer id the mock has subscriptions for |
| `UNKNOWN_CUSTOMER_REFERENCE` | `no_such_customer_ref` | A reference the mock 404s |
| `UNKNOWN_CUSTOMER_ID` | `99999999` | A well-formed but unknown numeric customer id |

If the PublicApi isn't reachable, tests fail with a clear message telling you to start it and point it at
the mock (rather than an opaque connection error).
