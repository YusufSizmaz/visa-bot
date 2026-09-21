using Microsoft.EntityFrameworkCore;
using VisaTelegramBot.Application.NewsSources;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Infrastructure.Persistence.Queries;

/// <summary>
/// Okuma tarafi: AsNoTracking ile degisiklik takibi kapatilir. EF Core nesneleri izlemek icin
/// ek bellek ve CPU harcamaz; salt okunur sorgularda ciddi performans kazanci saglar.
/// </summary>
internal sealed class NewsSourceQueries(AppDbContext dbContext) : INewsSourceQueries
{
    public async Task<NewsSourceResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var source = await dbContext.NewsSources
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return source is null ? null : Map(source);
    }

    public async Task<IReadOnlyList<NewsSourceResponse>> ListAsync(bool? isActive, CancellationToken cancellationToken)
    {
        var query = dbContext.NewsSources.AsNoTracking();

        if (isActive is not null)
        {
            query = query.Where(source => source.IsActive == isActive);
        }

        var sources = await query
            .OrderBy(source => source.Name)
            .ToListAsync(cancellationToken);

        return sources.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetDueIdsAsync(DateTime utcNow, int limit, CancellationToken cancellationToken)
    {
        // IX_NewsSources_IsActive_NextFetchAtUtc index'i bu sorguyu dogrudan karsilar.
        return await dbContext.NewsSources
            .AsNoTracking()
            .Where(source => source.IsActive && source.NextFetchAtUtc <= utcNow)
            .OrderBy(source => source.NextFetchAtUtc)
            .Select(source => source.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private static NewsSourceResponse Map(NewsSource source)
    {
        return new NewsSourceResponse(
            source.Id,
            source.Name,
            source.Url.Value,
            source.Type,
            HtmlParsingRulesDto.FromDomain(source.ParsingRules),
            source.KeywordFilter?.Keywords.ToArray(),
            source.TurkishOnly,
            source.Category,
            (int)source.FetchInterval.TotalMinutes,
            source.IsActive,
            source.CreatedAtUtc,
            source.NextFetchAtUtc,
            source.LastFetchedAtUtc,
            source.LastSucceededAtUtc,
            source.ConsecutiveFailureCount,
            source.LastFetchError);
    }
}
