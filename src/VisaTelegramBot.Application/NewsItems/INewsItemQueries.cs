using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Domain.NewsItems;

namespace VisaTelegramBot.Application.NewsItems;

public interface INewsItemQueries
{
    Task<CursorPage<NewsItemResponse>> ListAsync(NewsItemListFilter filter, CancellationToken cancellationToken);

    Task<NewsItemStatsResponse> GetStatsAsync(DateTime deliveredSinceUtc, CancellationToken cancellationToken);
}

public sealed record NewsItemStatsResponse(
    int Pending,
    int Delivered,
    int Failed,
    int Archived,
    int DeliveredLast24Hours,
    DateTime? LastDeliveredAtUtc,
    DateTime? LastDiscoveredAtUtc);

public sealed record NewsItemListFilter(
    Guid? NewsSourceId,
    DeliveryStatus? DeliveryStatus,
    NewsItemCursor? After,
    int PageSize);
