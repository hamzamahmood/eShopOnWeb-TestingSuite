namespace MaxioMock;

/// <summary>
/// Canned, spec-faithful Maxio fixtures + response builders (single-key envelopes, snake_case
/// wire fields). Builders return a fresh object each call, so lifecycle routes can return a
/// patched copy without mutating shared state (deterministic, order-independent).
///
/// Fixture identifiers are the experimenter's choice; the arm's config and the gate reference
/// these exact values. They are the ONLY place to change ids/handles.
/// </summary>
public static class MockStore
{
    // --- fixture identifiers (mirror into the arm's Maxio__* config and the gate) ---
    public const int    ProductFamilyId        = 600001;
    public const string ProductFamilyHandle    = "eshop-plans";
    public const int    MeteredComponentId     = 800001;
    public const string MeteredComponentHandle = "api-calls";

    public const string KnownCustomerReference = "cust_known";
    public const int    KnownCustomerId        = 900001;
    public const int    CreatedCustomerId      = 900002;

    public const int    SubActiveId            = 950001;
    public const int    SubOnHoldId            = 950002;
    public const int    SubCanceledId          = 950003;
    public const int    NewSubscriptionId      = 950010;
    public const long   UsageId                = 990001;

    private const string Ts = "2026-01-01T00:00:00-05:00";

    private static object ProductFamily() => new
    {
        id = ProductFamilyId,
        name = "eShop Plans",
        description = "eShopOnWeb subscription plans",
        handle = ProductFamilyHandle,
        accounting_code = (string?)null,
    };

    public static object Product(int id, string name, string handle, int priceInCents) => new
    {
        id,
        name,
        handle,
        description = "",
        accounting_code = "",
        request_credit_card = false,
        require_credit_card = false,
        price_in_cents = priceInCents,
        interval = 1,
        interval_unit = "month",
        initial_charge_in_cents = 0,
        trial_price_in_cents = (int?)null,
        trial_interval = (int?)null,
        trial_interval_unit = (string?)null,
        expiration_interval = (int?)null,
        expiration_interval_unit = "never",
        created_at = Ts,
        updated_at = Ts,
        archived_at = (string?)null,
        taxable = false,
        version_number = 1,
        product_price_point_id = id + 1,
        product_price_point_name = "Default",
        product_price_point_handle = (string?)null,
        product_family = ProductFamily(),
        public_signup_pages = Array.Empty<object>(),
    };

    public static object ProProduct()   => Product(700001, "Pro Plan",   "pro-plan",   29900);
    public static object BasicProduct() => Product(700002, "Basic Plan", "basic-plan",  2900);

    public static object? ProductByHandle(string? handle) => handle switch
    {
        "pro-plan"   => ProProduct(),
        "basic-plan" => BasicProduct(),
        _            => null,
    };

    /// <summary>GET products.json → a bare array of product envelopes: [{"product": {...}}, ...].</summary>
    public static object[] ProductsList() => new[]
    {
        new { product = ProProduct() },
        new { product = BasicProduct() },
    };

    public static object Customer(int id, string firstName, string lastName, string email, string? reference) => new
    {
        id,
        first_name = firstName,
        last_name = lastName,
        email,
        cc_emails = (string?)null,
        organization = (string?)null,
        reference,
        created_at = Ts,
        updated_at = Ts,
        address = (string?)null,
        address_2 = (string?)null,
        city = (string?)null,
        state = (string?)null,
        zip = (string?)null,
        country = (string?)null,
        phone = (string?)null,
        verified = false,
        tax_exempt = false,
        vat_number = (string?)null,
        parent_id = (int?)null,
        locale = (string?)null,
    };

    public static object KnownCustomer() =>
        Customer(KnownCustomerId, "Ada", "Lovelace", "ada@example.com", KnownCustomerReference);

    public static object Subscription(int id, string state, object product, object customer) => new
    {
        id,
        state,
        balance_in_cents = 0,
        total_revenue_in_cents = 0,
        product_price_in_cents = 29900,
        product_version_number = 1,
        current_period_started_at = Ts,
        current_period_ends_at = "2026-02-01T00:00:00-05:00",
        next_assessment_at = "2026-02-01T00:00:00-05:00",
        trial_started_at = (string?)null,
        trial_ended_at = (string?)null,
        activated_at = Ts,
        expires_at = (string?)null,
        created_at = Ts,
        updated_at = Ts,
        canceled_at = state == "canceled" ? "2026-01-15T00:00:00-05:00" : (string?)null,
        cancellation_message = (string?)null,
        cancellation_method = state == "canceled" ? "merchant_api" : (string?)null,
        cancel_at_end_of_period = false,
        previous_state = "active",
        signup_payment_id = 1,
        signup_revenue = "0.00",
        payment_collection_method = "automatic",
        payment_type = (string?)null,
        on_hold_at = state == "on_hold" ? "2026-01-10T00:00:00-05:00" : (string?)null,
        automatically_resume_at = (string?)null,
        currency = "USD",
        product,
        product_price_point_id = 700002,
        customer,
    };

    public static object ActiveSubscription()   => Subscription(SubActiveId,   "active",   ProProduct(), KnownCustomer());
    public static object OnHoldSubscription()   => Subscription(SubOnHoldId,   "on_hold",  ProProduct(), KnownCustomer());
    public static object CanceledSubscription() => Subscription(SubCanceledId, "canceled", ProProduct(), KnownCustomer());

    /// <summary>Look up a canned subscription by wire id, or null if unknown (→ 404).</summary>
    public static (object sub, string state)? SubscriptionById(string id)
    {
        if (id == SubActiveId.ToString())   return (ActiveSubscription(),   "active");
        if (id == SubOnHoldId.ToString())   return (OnHoldSubscription(),   "on_hold");
        if (id == SubCanceledId.ToString()) return (CanceledSubscription(), "canceled");
        return null;
    }

    public static object Usage(long id, object quantity, int componentId, int subscriptionId, string? memo) => new
    {
        id,
        memo,
        created_at = "2026-01-20T00:00:00-05:00",
        price_point_id = 149416,
        quantity,
        component_id = componentId,
        component_handle = MeteredComponentHandle,
        subscription_id = subscriptionId,
    };

    // ---- extended billing surface: components / price points / subscription-components / allocations / invoices / coupons ----
    public const int ComponentMeteredId  = 800001;   // "api-calls" (same as MeteredComponentId)
    public const int ComponentQtyId      = 800002;
    public const int CreatedComponentId  = 800010;
    public const int PricePointId        = 810001;
    public const int CreatedPricePointId = 810010;
    public const int SubComponentId      = 820001;
    public const int AllocationId        = 830001;
    public const int CreatedAllocationId = 830010;
    public const int CouponId            = 840001;
    public const int CreatedCouponId     = 840010;

    public static object Component(int id, string name, string handle, string kind, string unitName, string unitPrice) => new
    {
        id, name, handle, kind,
        unit_name = unitName, unit_price = unitPrice, pricing_scheme = "per_unit",
        product_family_id = ProductFamilyId, archived = false, taxable = false,
        default_price_point_id = PricePointId, price_point_count = 1, recurring = false, created_at = Ts,
    };
    public static object MeteredComponent() => Component(ComponentMeteredId, "API Calls", "api-calls", "metered_component", "call", "0.01");
    public static object QtyComponent()     => Component(ComponentQtyId, "Seats", "seats", "quantity_based_component", "seat", "10.0");
    public static object[] ComponentsList() => new[] { new { component = MeteredComponent() }, new { component = QtyComponent() } };
    public static object? ComponentById(string cid)
    {
        if (cid == ComponentMeteredId.ToString()) return MeteredComponent();
        if (cid == ComponentQtyId.ToString())     return QtyComponent();
        return null;
    }

    public static object PricePoint(int id, string name, int componentId) => new
    {
        id, name, handle = (string?)null, pricing_scheme = "per_unit", component_id = componentId,
        type = "catalog", @default = false,
        prices = new[] { new { id = id + 500, component_id = componentId, starting_quantity = 1, ending_quantity = (int?)null, unit_price = "5.0" } },
        archived_at = (string?)null, created_at = Ts, updated_at = Ts,
    };
    public static object PricePointsList(int componentId) => new
    {
        price_points = new[] { PricePoint(PricePointId, "Default", componentId), PricePoint(PricePointId + 1, "Volume", componentId) },
        meta = new { total_count = 2, current_page = 1, total_pages = 1, per_page = 30 },
    };

    public static object SubscriptionComponent(int id, int componentId, int subId, string name, int allocated) => new
    {
        id, component_id = componentId, subscription_id = subId, name,
        kind = "metered_component", unit_name = "call", allocated_quantity = allocated, pricing_scheme = "per_unit",
        enabled = true, unit_balance = 0, price_point_id = PricePointId, price_point_handle = "default",
        price_point_type = "default", price_point_name = "Default", component_handle = "api-calls",
        created_at = Ts, updated_at = Ts, archived_at = (string?)null,
    };
    public static object[] SubscriptionComponentsList(int subId) => new[] { new { component = SubscriptionComponent(SubComponentId, ComponentMeteredId, subId, "API Calls", 5) } };

    public static object Allocation(int allocationId, int componentId, int subId, object quantity, string? memo) => new
    {
        allocation_id = allocationId, component_id = componentId, subscription_id = subId,
        quantity, previous_quantity = 0, memo, timestamp = "2026-01-20T00:00:00Z", price_point_id = PricePointId,
        component_handle = "api-calls", accrue_charge = false, upgrade_charge = "full", downgrade_credit = "none",
        created_at = Ts, used_quantity = 0,
    };
    public static object[] AllocationsList(int subId) => new[] { new { allocation = Allocation(AllocationId, ComponentMeteredId, subId, 5, "initial") } };

    public static object Invoice(string uid, string number, int subId, string total, string status) => new
    {
        uid, id = 700100, number, sequence_number = 1, status, total_amount = total, subtotal_amount = total,
        discount_amount = "0.0", tax_amount = "0.0", paid_amount = "0.0", due_amount = total, currency = "USD",
        subscription_id = subId, customer_id = KnownCustomerId, issue_date = "2026-01-01", due_date = "2026-01-01",
        paid_date = (string?)null, collection_method = "automatic", public_url = "https://acme.chargify.com/invoice/inv_abc001",
    };
    public static object InvoicesList(int subId) => new { invoices = new[] { Invoice("inv_abc001", "1", subId, "299.00", "open") } };

    public static object Coupon(int id, string code, string name, string percentage) => new
    {
        id, name, code, description = name, percentage, amount_in_cents = (int?)null, amount = (double?)null,
        product_family_id = ProductFamilyId, recurring = true, start_date = Ts, end_date = (string?)null,
        archived_at = (string?)null, stackable = false, use_site_exchange_rate = true,
    };
    public static object[] CouponsList() => new[] { new { coupon = Coupon(CouponId, "SAVE10", "10% off", "10") } };
}
