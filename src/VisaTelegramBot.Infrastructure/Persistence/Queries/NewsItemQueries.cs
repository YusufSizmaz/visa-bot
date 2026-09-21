using Microsoft.EntityFrameworkCore;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Application.NewsItems;
using VisaTelegramBot.Domain.NewsItems;

namespace VisaTelegramBot.Infrastructure.Persistence.Queries;

internal sealed class NewsItemQueries(AppDbContext dbContext) : INewsItemQueries
{
    public async Task<NewsItemStatsResponse> GetStatsAsync(DateTime deliveredSinceUtc, CancellationToken cancellationToken)
    {
        // Tek GROUP BY sorgusu: her durum icin ayri COUNT atmak yerine.
        var counts = await dbContext.NewsItems
            .AsNoTracking()
            .GroupBy(item => item.DeliveryStatus)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Status, row => row.Count, cancellationToken);

        var deliveredRecently = await dbContext.NewsItems
            .AsNoTracking()
            .CountAsync(item => item.DeliveredAtUtc >= deliveredSinceUtc, cancellationToken);

        var lastDeliveredAtUtc = await dbContext.NewsItems
            .AsNoTracking()
            .MaxAsync(item => item.DeliveredAtUtc, cancellationToken);

        var lastDiscoveredAtUtc = await dbContext.NewsItems
            .AsNoTracking()
            .Select(item => (DateTime?)item.DiscoveredAtUtc)
            .MaxAsync(cancellationToken);

        return new NewsItemStatsResponse(
            counts.GetValueOrDefault(DeliveryStatus.Pending),
            counts.GetValueOrDefault(DeliveryStatus.Delivered),
            counts.GetValueOrDefault(DeliveryStatus.Failed),
            counts.GetValueOrDefault(DeliveryStatus.Archived),
            deliveredRecently,
            lastDeliveredAtUtc,
            lastDiscoveredAtUtc);
    }

    public async Task<CursorPage<NewsItemResponse>> ListAsync(NewsItemListFilter filter, CancellationToken cancellationToken)
    {
        var query = dbContext.NewsItems.AsNoTracking();

        if (filter.NewsSourceId is not null)
        {
            query = query.Where(item => item.NewsSourceId == filter.NewsSourceId);
        }

        if (filter.DeliveryStatus is not null)
        {
            query = query.Where(item => item.DeliveryStatus == filter.DeliveryStatus);
        }

        if (filter.After is { } after)
        {
            // Keyset sayfalama: "OFFSET 10000" yerine son gorulen kaydin anahtarindan devam et.
            // SQL Server bu kosulu clustered index uzerinde dogrudan arama (seek) ile karsilar.
            query = query.Where(item =>
                item.DiscoveredAtUtc < after.DiscoveredAtUtc
                || (item.DiscoveredAtUtc == after.DiscoveredAtUtc && item.Id.CompareTo(after.Id) < 0));
        }

        // Bir fazlasini cekeriz: gelirse bir sonraki sayfa var demektir. COUNT(*) sorgusuna gerek kalmaz.
        var rows = await query
            .Join(
                dbContext.NewsSources.AsNoTracking(),
                item => item.NewsSourceId,
                source => source.Id,
                (item, source) => new { Item = item, SourceName = source.Name })
            .OrderByDescending(row => row.Item.DiscoveredAtUtc)
            .ThenByDescending(row => row.Item.Id)
            .Take(filter.PageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > filter.PageSize;
        var pageRows = hasMore ? rows.Take(filter.PageSize).ToList() : rows;

        var items = pageRows
            .Select(row => new NewsItemResponse(
                row.Item.Id,
                row.Item.NewsSourceId,
                row.SourceName,
                row.Item.Title,
                row.Item.Url.Value,
                row.Item.Summary,
                row.Item.PublishedAtUtc,
                row.Item.DiscoveredAtUtc,
                row.Item.DeliveryStatus,
                row.Item.DeliveryAttempts,
                row.Item.NextDeliveryAttemptAtUtc,
                row.Item.DeliveredAtUtc,
                row.Item.LastDeliveryError,
                row.Item.ArchiveReason))
            .ToList();

        var last = pageRows.Count > 0 ? pageRows[^1].Item : null;

        var nextCursor = hasMore && last is not null
            ? new NewsItemCursor(last.DiscoveredAtUtc, last.Id).Encode()
            : null;

        return new CursorPage<NewsItemResponse>(items, nextCursor);
    }
}
