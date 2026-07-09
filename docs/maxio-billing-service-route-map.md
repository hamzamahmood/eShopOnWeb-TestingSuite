# Maxio Endpoint → Controller Route Map (for `MaxioBillingController` generation)

Deterministic map for exposing the underlying Maxio billing service as HTTP routes. The service is the
`MaxioBillingClient` concrete class (`src/Infrastructure/Services/MaxioBillingClient.cs` — flattened, no
`Maxio/` subfolder) implementing the provider-agnostic billing seam `IBillingClient`
(`src/ApplicationCore/Interfaces/IBillingClient.cs`). Both files are guaranteed present as inputs to
controller generation. The generated `MaxioBillingController` lives in the standalone **`MaxioBillingTestApi`**
host (Direct: `src/MaxioBillingTestApi/`; Plugin: repo-root `MaxioBillingTestApi/`), not in `PublicApi`. Each
row pairs the **upstream Maxio Advanced Billing endpoint** that a service method calls with the
**`MaxioBillingController` route** that must be generated to front it, so outside callers can invoke that
service method over HTTP.

An AI agent generating or regenerating `MaxioBillingController` should treat this table as the source of
truth for routing: for every Maxio endpoint a service method depends on, wire up the controller endpoint
route listed here — do not invent a different route, and do not hand-declare `[Http*]` attributes ad hoc.

| Maxio endpoint route | Controller endpoint route |
|---|---|
| `GET /product_families/{product_family_id}/products.json` | `GET api/maxio/product-families/{productFamilyId}/products` |
| `GET /customers/lookup.json` → `POST /customers.json` | `POST api/maxio/customers` |
| `GET /customers/{customer_id}/subscriptions.json` | `GET api/maxio/customers/{customerId}/subscriptions` |
| `POST /subscriptions.json` | `POST api/maxio/subscriptions` |
| `GET /subscriptions/{subscription_id}.json` | `GET api/maxio/subscriptions/{subscriptionId}` |
| `POST /subscriptions/{subscription_id}/migrations/preview.json` **†** | `POST api/maxio/subscriptions/{subscriptionId}/migrations/preview` |
| `POST /subscriptions/{subscription_id}/migrations.json` (`preserve_period=true`) **†** | `POST api/maxio/subscriptions/{subscriptionId}/migrations` |
| `POST /subscriptions/{subscription_id}/hold.json` | `POST api/maxio/subscriptions/{subscriptionId}/hold` |
| `POST /subscriptions/{subscription_id}/resume.json` | `POST api/maxio/subscriptions/{subscriptionId}/resume` |
| `DELETE /subscriptions/{subscription_id}.json` **†** | `DELETE api/maxio/subscriptions/{subscriptionId}` |
| `PUT /subscriptions/{subscription_id}/reactivate.json` | `PUT api/maxio/subscriptions/{subscriptionId}/reactivate` |
| `GET /product_families/{product_family_id}/components/{component_id}.json` | `GET api/maxio/metered-component` |
| `GET /subscriptions/{subscription_id}/components/{component_id}.json` | `GET api/maxio/subscriptions/{subscriptionId}/component-balance` |
| `GET /product_families/{id}/components/{comp}.json` (guard) → `POST /subscriptions/{subscription_id}/components/{component_id}/usages.json` | `POST api/maxio/subscriptions/{subscriptionIdOrReference}/usages` |
| `PUT /subscriptions/{subscription_id}.json` (`product_change_delayed=true`) | `PUT api/maxio/subscriptions/{subscriptionId}` |
| `POST /subscriptions/{subscription_id}/delayed_cancel.json` | `POST api/maxio/subscriptions/{subscriptionId}/delayed_cancel` |
| `GET /customers/lookup.json` | `GET api/maxio/customers/lookup?reference=` |
| `GET /components/lookup.json?handle={handle}` | `GET api/maxio/metered-component/verify` |
| `POST /subscriptions/{subscription_id}/components/{component_id}/usages.json` | `POST api/maxio/subscriptions/{subscriptionId}/components/{componentId}/usages` |
| `GET /subscriptions/{subscription_id}/components/{component_id}.json` **+** `GET /subscriptions/{subscription_id}.json` | `GET api/maxio/subscriptions/{subscriptionId}/components/{componentId}/summary` |
