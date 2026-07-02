using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;
using Microsoft.Extensions.Caching.Memory;

namespace Microsoft.eShopWeb.Infrastructure.Services;

/// <summary>
/// Backs <see cref="IIdempotencyCache"/> with the process's <see cref="IMemoryCache"/> (already registered by
/// both hosts via <c>AddMemoryCache()</c>). Single-instance, best-effort only - see the interface's remarks.
/// </summary>
public class MemoryIdempotencyCache : IIdempotencyCache
{
    private static readonly TimeSpan ClaimWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PreviewWindow = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _cache;

    public MemoryIdempotencyCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool TryClaim(string key)
    {
        var cacheKey = $"idempotency:{key}";
        if (_cache.TryGetValue(cacheKey, out _))
        {
            return false;
        }

        _cache.Set(cacheKey, true, ClaimWindow);
        return true;
    }

    public string StorePreview(ProrationPreviewDto preview)
    {
        var token = Guid.NewGuid().ToString("N");
        _cache.Set($"preview:{token}", preview, PreviewWindow);
        return token;
    }

    public ProrationPreviewDto? TakePreview(string token)
    {
        var cacheKey = $"preview:{token}";
        if (_cache.TryGetValue(cacheKey, out ProrationPreviewDto? preview))
        {
            _cache.Remove(cacheKey);
            return preview;
        }

        return null;
    }
}
