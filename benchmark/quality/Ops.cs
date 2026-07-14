namespace Quality;

/// <summary>What the oracle should assert about a response AFTER a drift is applied.</summary>
public enum DriftCheck
{
    Values,   // the op's usual required values must still appear (else SILENT-WRONG on 2xx)
    NewEnum,  // an unmodeled enum value: CORRECT if the new value is surfaced; GRACEFUL if cleanly rejected
    Units,    // a dollars-consistent price must still appear (price-field drift)
}

/// <summary>One drift scenario applied to an op. Field/To target the mock's wire JSON. Expect overrides
/// the required values post-drift (default = the op's MustContain).</summary>
public sealed record DriftCase(string Label, string Profile, string? Field, string? To, DriftCheck Check, string[]? Expect = null);

/// <summary>
/// An operation the quality instruments exercise. AppPath is the pinned eShop route with fixture ids
/// substituted; Upstream is the Maxio wire-path fragment the drift engine targets. MustContain are the
/// values a correct response must surface (field-name- and representation-agnostic, like the Stage-1
/// gate) — DEEPENED here to id + state + plan, list cardinality (two ids), etc. ExpectDollars adds the
/// cents-vs-dollars unit check the gate deliberately omitted. Drift targets are chosen so the drifted
/// field carries a MustContain value ⇒ a silent drop is detectable.
/// </summary>
public sealed record QOp(
    string Id, string Method, string AppPath, string? Body, string Upstream,
    string[] MustContain, double? ExpectDollars, DriftCase[] Drifts, int Scope = 11);

public static class Ops
{
    // fixture values mirror mock/MockStore.cs. Scope = the smallest task size that includes the op
    // (11 = present in every tree; 22 = extended surface, absent from the 11-op pilot trees).
    public static readonly QOp[] All =
    {
        new("plans", "GET", "/api/billing/plans", null, "products.json",
            MustContain: new[] { "700001", "700002" },          // both plans present (cardinality)
            ExpectDollars: 299.00,                               // Pro Plan = 29900 cents = $299.00
            Drifts: new[]
            {
                new DriftCase("additive",        "additive", null, null, DriftCheck.Values),
                new DriftCase("envelope→plan",   "envelope", "product", "plan", DriftCheck.Values),
                new DriftCase("retype id→string","retype",   "id", null, DriftCheck.Values),
                new DriftCase("rename price",    "rename",   "price_in_cents", "price_cents", DriftCheck.Units),
            }),

        new("read-sub", "GET", "/api/billing/subscriptions/950001", null, "/subscriptions/950001.json",
            MustContain: new[] { "950001", "active", "pro-plan" }, // id + state + plan (deeper than gate)
            ExpectDollars: null,
            Drifts: new[]
            {
                new DriftCase("additive",         "additive", null, null, DriftCheck.Values),
                new DriftCase("rename state",     "rename",   "state", "sub_state", DriftCheck.Values),
                new DriftCase("new-enum state",   "newenum",  "state", "paused_pending", DriftCheck.NewEnum, new[] { "950001", "paused_pending" }),
                new DriftCase("envelope→sub",     "envelope", "subscription", "sub", DriftCheck.Values),
            }),

        new("list-subs", "GET", "/api/billing/customers/900001/subscriptions", null, "/customers/900001/subscriptions.json",
            MustContain: new[] { "950001", "active" },
            ExpectDollars: null,
            Drifts: new[]
            {
                new DriftCase("additive",     "additive", null, null, DriftCheck.Values),
                new DriftCase("rename state", "rename",   "state", "sub_state", DriftCheck.Values),
            }),

        new("create-sub", "POST", "/api/billing/subscriptions",
            "{\"customerReference\":\"cust_known\",\"productHandle\":\"pro-plan\"}", "/subscriptions.json",
            MustContain: new[] { "950010", "active" },
            ExpectDollars: null,
            Drifts: new[]
            {
                new DriftCase("additive",       "additive", null, null, DriftCheck.Values),
                new DriftCase("new-enum state", "newenum",  "state", "trial_pending", DriftCheck.NewEnum, new[] { "950010", "trial_pending" }),
                new DriftCase("rename id",      "rename",   "id", "sub_id", DriftCheck.Values),
            }),

        new("allocations", "GET", "/api/billing/subscriptions/950001/components/800001/allocations", null, "/allocations.json",
            MustContain: new[] { "830001" },                    // allocation id lives in wire field "allocation_id"
            ExpectDollars: null,
            Drifts: new[]
            {
                new DriftCase("additive",             "additive", null, null, DriftCheck.Values),
                new DriftCase("rename allocation_id", "rename",   "allocation_id", "alloc_id", DriftCheck.Values),
            }, Scope: 22),

        new("invoices", "GET", "/api/billing/subscriptions/950001/invoices", null, "/invoices.json",
            MustContain: new[] { "inv_abc001", "open" },        // uid + status
            ExpectDollars: 299.00,                               // total_amount "299.00"
            Drifts: new[]
            {
                new DriftCase("additive",       "additive", null, null, DriftCheck.Values),
                new DriftCase("rename status",  "rename",   "status", "invoice_state", DriftCheck.Values),
                new DriftCase("rename total",   "rename",   "total_amount", "total", DriftCheck.Units),
                new DriftCase("envelope→list",  "envelope", "invoices", "invoice_list", DriftCheck.Values),
            }, Scope: 22),

        new("components", "GET", "/api/billing/components", null, "/product_families/600001/components.json",
            MustContain: new[] { "800001", "800002" },
            ExpectDollars: null,
            Drifts: new[]
            {
                new DriftCase("additive",        "additive", null, null, DriftCheck.Values),
                new DriftCase("envelope→comp",   "envelope", "component", "comp", DriftCheck.Values),
                new DriftCase("retype id→string","retype",   "id", null, DriftCheck.Values),
            }, Scope: 22),
    };
}
