# SubscriptionService Comparison: Plugin vs Direct

Comparing:
- `eShopOnWeb-Plugin\src\ApplicationCore\Services\SubscriptionService.cs`
- `eShopOnWeb-Direct\src\ApplicationCore\Services\SubscriptionService.cs`

## Summary

Both implement `ISubscriptionService`, but they follow fundamentally different architectural models: **Plugin is stateless (billing provider as source of truth)**, while **Direct is stateful (local DB entities kept in sync with the provider)**.

## Architecture / persistence
- **Plugin** (`SubscriptionService.cs:15-18`): only depends on `IBillingClient`, `IPublisher`, `IIdempotencyCache`. No local database — every read/write round-trips to the billing provider directly (e.g. `ReadSubscriptionAsync`, `ListCustomerSubscriptionsAsync`).
- **Direct** (`SubscriptionService.cs:18-22`): additionally depends on `IRepository<Subscription>` and `IRepository<UsageRecord>`. It persists a local `Subscription` entity on subscribe (`:63-64`) and calls `subscription.SyncFromProvider(...)` + `UpdateAsync` after every mutation (`:157-158`, `:216-217`, `:241-242`) — the local DB is a cached/synced projection of provider state, and subscription IDs are local `int` surrogate keys, separate from the provider's string ID.

## Input validation
- **Direct** uses `Ardalis.GuardClauses` throughout (`Guard.Against.NullOrEmpty`) and adds real business rules Plugin doesn't have: checks the target plan actually exists (`:49-54`), enforces `RequiresPaymentMethod` + payment token (`:55-58`), rejects zero-quantity usage (`:94-97`), and guards that a plan-change target differs from the current plan (`GuardTargetPlanIsDifferent`, `:246-253`).
- **Plugin** has none of these checks — it's a thin pass-through to the billing client.

## Idempotency
- **Plugin** (`:62-66`) uses an in-memory `IIdempotencyCache.TryClaim` keyed on `"usage:{subscriptionId}:{requestId}"` — non-durable.
- **Direct** (`:101-108`) persists a `UsageRecord` per idempotency key and looks it up via a specification — durable, survives restarts.

## Plan-change preview/commit
- **Plugin** uses an opaque **preview-token** pattern: `PreviewPlanChangeAsync` stashes the quote in `_idempotencyCache` and returns a token (`:104-106`); `CommitPlanChangeAsync` takes that token back, re-fetches a fresh quote, and compares `ProratedAmount` to detect staleness (`:113-123`).
- **Direct** has no token/cache — the caller passes `expectedProratedAdjustmentInCents` directly, compared against a freshly computed preview (`:145-148`). It also distinguishes `Now` vs `AtRenewal` timing explicitly, calling different billing methods and returning `null` for `AtRenewal` since no proration applies (`:128-130`).

## State-transition checks
- **Plugin** uses a `SubscriptionState` enum and `IllegalSubscriptionTransitionException` (e.g. `:136-139`).
- **Direct** uses raw string states (`"on_hold"`, `"canceled"`, etc.) and a generic `InvalidSubscriptionStateException` with a message. The business rule also differs: Plugin's `ReactivateAsync` only allows reactivation from `Canceled` (`:175`), while Direct also allows `"unpaid"` and `"trial_ended"` (`:203-206`) — a broader rule.

## Event publishing
- **Plugin** publishes events with a bare `_publisher.Publish(...)` — a handler exception would propagate and fail the whole request.
- **Direct** wraps every publish in `PublishBestEffortAsync`, which catches and logs (`:255-267`), explicitly per a documented rule ("plan.md section 2.5") that in-process notification failures must never roll back an already-successful billing action. Plugin has no equivalent safeguard.

## Ownership check
Both implement the identical security pattern (same exception for "not found" and "not yours" so a non-owner can't distinguish the two), but:
- Plugin's `GetOwnedSubscriptionAsync` (`:185-196`) hits the billing provider directly by string ID.
- Direct's `LoadOwnedSubscriptionAsync` (`:224-236`) reads the local repository by int ID and also guards `actorBuyerId` for null/empty.

## Feature present only in Plugin
`RecordUsageForUserIfSubscribedAsync` (`:73-91`) — looks up a user's active subscription and records usage automatically; Direct has no equivalent. Plugin's usage recording also calls `VerifyMeteredComponentAsync` first (`:59`) to confirm the plan has a metered component configured; Direct skips this check.

## DTOs
Plugin returns billing-client DTOs almost verbatim (`SubscriptionDto`, `UsageDto`, `PlanDto`). Direct wraps them in domain types that pair the local entity ID with the provider model (`SubscriptionSummary`, `PlanChangeResult`), reflecting its stateful design.
