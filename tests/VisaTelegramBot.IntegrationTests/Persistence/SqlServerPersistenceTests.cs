using Microsoft.EntityFrameworkCore;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.NewsItems;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsItems;
using VisaTelegramBot.Domain.NewsSources;
using VisaTelegramBot.Infrastructure.Locking;
using VisaTelegramBot.Infrastructure.Persistence.Queries;
using VisaTelegramBot.Infrastructure.Persistence.Repositories;

namespace VisaTelegramBot.IntegrationTests.Persistence;

[Collection(SqlServerCollection.Name)]
public sealed class SqlServerPersistenceTests(SqlServerFixture fixture)
{
    private static readonly DateTime Now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    [SqlServerFact]
    public async Task NewsSource_RoundTripsValueObjectsAndUtcDates()
    {
        var rules = HtmlParsingRules.Create("article", "h2", "a", "p", "time");
        var source = NewsSource.Create("HTML", UniqueUrl(), SourceType.Html, rules, TimeSpan.FromHours(24), Now);
        source.RecordFetchFailure("timeout", Now.AddMinutes(1));

        await SaveAsync(db => db.NewsSources.Add(source));

        await using var readContext = fixture.CreateDbContext();
        var loaded = await readContext.NewsSources.SingleAsync(item => item.Id == source.Id);

        Assert.Equal(rules, loaded.ParsingRules);
        Assert.Equal(source.Url, loaded.Url);
        Assert.Equal(TimeSpan.FromHours(24), loaded.FetchInterval);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, loaded.LastFetchedAtUtc!.Value.Kind);
        Assert.Equal(Now.AddMinutes(1), loaded.LastFetchedAtUtc);
    }

    [SqlServerFact]
    public async Task DuplicateContentHash_IsRejectedByUniqueIndex()
    {
        var source = await CreateSourceAsync();
        var url = UniqueUrl();

        await SaveAsync(db => db.NewsItems.Add(NewsItem.Discover(source.Id, "Bir", url, null, null, Now)));

        await using var dbContext = fixture.CreateDbContext();
        dbContext.NewsItems.Add(NewsItem.Discover(source.Id, "İki", WebUrl.Create(url.Value + "?utm_source=x"), null, null, Now.AddTicks(1)));

        await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => SqlServerFixture.CreateUnitOfWork(dbContext).SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task ConcurrentClaims_NeverReturnSameItem()
    {
        var source = await CreateSourceAsync();
        var items = Enumerable.Range(0, 20)
            .Select(i => NewsItem.Discover(source.Id, $"Haber {i}", UniqueUrl(), null, null, Now.AddTicks(i)))
            .ToList();

        await SaveAsync(db => db.NewsItems.AddRange(items));

        var claimAt = Now.AddMinutes(1);

        var claims = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            await using var dbContext = fixture.CreateDbContext();
            return await new NewsItemRepository(dbContext).ClaimForDeliveryAsync(100, claimAt, claimAt.AddMinutes(5), CancellationToken.None);
        }));

        var claimedIds = claims.SelectMany(ids => ids).Where(id => items.Any(item => item.Id == id)).ToList();

        Assert.Equal(claimedIds.Count, claimedIds.Distinct().Count());
        Assert.Equal(items.Count, claimedIds.Count);

        // Kira suresi dolmadan tekrar alinamaz, dolduktan sonra alinabilir.
        await using var againContext = fixture.CreateDbContext();
        var repository = new NewsItemRepository(againContext);

        var beforeExpiry = await repository.ClaimForDeliveryAsync(100, claimAt.AddMinutes(4), claimAt.AddMinutes(10), CancellationToken.None);
        var afterExpiry = await repository.ClaimForDeliveryAsync(100, claimAt.AddMinutes(6), claimAt.AddMinutes(11), CancellationToken.None);

        Assert.DoesNotContain(beforeExpiry, id => items.Any(item => item.Id == id));
        Assert.Equal(items.Count, afterExpiry.Count(id => items.Any(item => item.Id == id)));
    }

    [SqlServerFact]
    public async Task DistributedLock_IsExclusiveUntilReleased()
    {
        var provider = new SqlServerDistributedLockProvider(fixture.ConnectionString);
        var resource = $"test-lock:{Guid.NewGuid():N}";

        var first = await provider.TryAcquireAsync(resource, CancellationToken.None);
        var second = await provider.TryAcquireAsync(resource, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Null(second);

        await first!.DisposeAsync();

        var third = await provider.TryAcquireAsync(resource, CancellationToken.None);
        Assert.NotNull(third);
        await third!.DisposeAsync();
    }

    [SqlServerFact]
    public async Task KeysetPagination_ReturnsEveryItemExactlyOnce()
    {
        var source = await CreateSourceAsync();

        // Ayni kesif zamanina sahip kayitlar da dahil: sayfa sinirinda kayip veya tekrar olmamali.
        var items = Enumerable.Range(0, 25)
            .Select(i => NewsItem.Archive(source.Id, $"Haber {i}", UniqueUrl(), null, null, Now.AddTicks(i % 5)))
            .ToList();

        await SaveAsync(db => db.NewsItems.AddRange(items));

        var seen = new List<Guid>();
        NewsItemCursor? cursor = null;

        do
        {
            await using var dbContext = fixture.CreateDbContext();
            var page = await new NewsItemQueries(dbContext).ListAsync(
                new NewsItemListFilter(source.Id, null, cursor, PageSize: 7),
                CancellationToken.None);

            seen.AddRange(page.Items.Select(item => item.Id));
            cursor = page.NextCursor is null ? null : NewsItemCursor.TryDecode(page.NextCursor, out var next) ? next : null;
        }
        while (cursor is not null);

        Assert.Equal(items.Count, seen.Count);
        Assert.Equal(items.Count, seen.Distinct().Count());
    }

    private async Task<NewsSource> CreateSourceAsync()
    {
        var source = NewsSource.Create("Kaynak", UniqueUrl(), SourceType.Rss, null, TimeSpan.FromMinutes(15), Now);
        await SaveAsync(db => db.NewsSources.Add(source));
        return source;
    }

    private async Task SaveAsync(Action<Infrastructure.Persistence.AppDbContext> change)
    {
        await using var dbContext = fixture.CreateDbContext();
        change(dbContext);
        await SqlServerFixture.CreateUnitOfWork(dbContext).SaveChangesAsync();
    }

    private static WebUrl UniqueUrl() => WebUrl.Create($"https://example.com/{Guid.NewGuid():N}");
}
