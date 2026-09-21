using Microsoft.Extensions.Caching.Hybrid;
using VisaTelegramBot.Application.Abstractions.Caching;

namespace VisaTelegramBot.Infrastructure.Caching;

/// <summary>
/// HybridCache iki katmanlidir: once process bellegi (L1), sonra Redis (L2).
/// Ayni anahtar icin es zamanli gelen yuzlerce istekten sadece biri veritabanina gider.
/// </summary>
internal sealed class HybridCacheService(HybridCache cache) : ICacheService
{
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan expiration,
        IReadOnlyCollection<string>? tags,
        CancellationToken cancellationToken)
    {
        var options = new HybridCacheEntryOptions
        {
            Expiration = expiration,
            LocalCacheExpiration = expiration < CachingDefaults.MaxLocalExpiration ? expiration : CachingDefaults.MaxLocalExpiration
        };

        return await cache.GetOrCreateAsync(
            key,
            factory,
            static async (state, token) => await state(token),
            options,
            tags,
            cancellationToken);
    }

    public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken)
    {
        return cache.RemoveByTagAsync(tag, cancellationToken).AsTask();
    }
}

internal static class CachingDefaults
{
    /// <summary>
    /// Birden fazla API kopyasinda bir kopyanin gecersiz kildigi veri, digerlerinin belleginde en fazla bu kadar kalir.
    /// </summary>
    public static readonly TimeSpan MaxLocalExpiration = TimeSpan.FromMinutes(1);
}
