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
}
