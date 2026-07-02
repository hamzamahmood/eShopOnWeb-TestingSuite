using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Short-lived, single-instance, best-effort de-duplication for operations the billing provider's API
/// declares no idempotency-key mechanism for (see integration plan's Maxio SDK facts). Backed by an
/// in-memory cache in <c>Infrastructure</c> — not a durable/distributed guarantee, matching this repo's
/// existing in-memory-only patterns.
/// </summary>
public interface IIdempotencyCache
{
    /// <summary>
    /// Returns <c>true</c> the first time <paramref name="key"/> is seen within the dedup window (the caller
    /// should proceed); <c>false</c> if it was already claimed (the caller should treat this as a repeat and
    /// skip the side-effecting call).
    /// </summary>
    bool TryClaim(string key);

    /// <summary>Stores a plan-change preview, returning the token <see cref="TakePreview"/> retrieves it with.</summary>
    string StorePreview(ProrationPreviewDto preview);

    /// <summary>Retrieves and removes a previously stored preview; <c>null</c> if missing or expired.</summary>
    ProrationPreviewDto? TakePreview(string token);
}
