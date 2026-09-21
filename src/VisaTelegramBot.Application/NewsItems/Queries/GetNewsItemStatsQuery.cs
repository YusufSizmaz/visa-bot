using VisaTelegramBot.Application.Abstractions.Caching;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Common.Results;

namespace VisaTelegramBot.Application.NewsItems.Queries;

/// <summary>Yonetim paneli ana sayfasi icin ozet sayilar.</summary>
public sealed record GetNewsItemStatsQuery : IQuery<NewsItemStatsResponse>;

internal sealed class GetNewsItemStatsQueryHandler(
    INewsItemQueries queries,
    ICacheService cache,
    TimeProvider timeProvider) : IQueryHandler<GetNewsItemStatsQuery, NewsItemStatsResponse>
{
    // Panel birkac saniyede bir yenilenir. Kisa bir cache, cok sayida sekme acik olsa bile veritabanini korur.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    public async Task<Result<NewsItemStatsResponse>> Handle(GetNewsItemStatsQuery query, CancellationToken cancellationToken)
    {
        var stats = await cache.GetOrCreateAsync(
            "news-items:stats",
            token => queries.GetStatsAsync(timeProvider.GetUtcNow().UtcDateTime.AddHours(-24), token),
            CacheDuration,
            tags: null,
            cancellationToken);

        return Result.Success(stats);
    }
}
