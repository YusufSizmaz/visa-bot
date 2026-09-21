using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsItems;
using VisaTelegramBot.Domain.NewsSources;
using VisaTelegramBot.Infrastructure.Persistence;
using VisaTelegramBot.Infrastructure.Persistence.Converters;

namespace VisaTelegramBot.IntegrationTests.Persistence;

/// <summary>
/// Veritabani gerektirmeyen model testleri. EF Core modelinin ve LINQ cevirilerinin dogru kuruldugunu dogrular.
/// </summary>
public sealed class ModelTests : IDisposable
{
    private readonly AppDbContext _dbContext = new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=ModelOnly;Username=postgres;Password=model-only")
            .Options);

    [Theory]
    [InlineData(typeof(NewsSource), nameof(NewsSource.CreatedAtUtc))]
    [InlineData(typeof(NewsSource), nameof(NewsSource.LastFetchedAtUtc))]
    [InlineData(typeof(NewsItem), nameof(NewsItem.PublishedAtUtc))]
    [InlineData(typeof(NewsItem), nameof(NewsItem.DeliveryLockedUntilUtc))]
    public void DateTimeProperties_UseUtcConverter_IncludingNullable(Type entityType, string propertyName)
    {
        var property = _dbContext.Model.FindEntityType(entityType)!.FindProperty(propertyName)!;

        Assert.IsType<UtcDateTimeConverter>(property.GetValueConverter());
    }

    [Fact]
    public void NewsItems_KeysetIndexIsTimeOrdered_NotGuidOnly()
    {
        // Index ayrintilari calisma zamani modelinde tutulmaz; migration'larin kullandigi tasarim modeline bakariz.
        var entity = _dbContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(NewsItem))!;

        // Keyset sayfalama (DiscoveredAtUtc, Id) sirasina guvenir; bu tekil index olmadan sayfalama bozulur.
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(NewsItem.DiscoveredAtUtc), nameof(NewsItem.Id)]));
    }

    [Fact]
    public void NewsItems_UseXminAsConcurrencyToken()
    {
        var entity = _dbContext.Model.FindEntityType(typeof(NewsItem))!;

        // PostgreSQL'de surum damgasi ayri bir kolon degil, sistem kolonu "xmin".
        Assert.Contains(entity.GetProperties(), property => property.IsConcurrencyToken && property.GetColumnName() == "xmin");
    }

    [Fact]
    public void ContentHashLookup_TranslatesToSql()
    {
        var hashes = new List<ContentHash> { ContentHash.FromUrl(WebUrl.Create("https://example.com/a")) };

        var sql = _dbContext.NewsItems.Where(item => hashes.Contains(item.ContentHash)).ToQueryString();

        Assert.Contains("ContentHash", sql);
    }

    [Fact]
    public void KeysetPredicate_TranslatesToSql()
    {
        var at = DateTime.UtcNow;
        var id = Guid.NewGuid();

        var sql = _dbContext.NewsItems
            .Where(item => item.DiscoveredAtUtc < at || (item.DiscoveredAtUtc == at && item.Id.CompareTo(id) < 0))
            .ToQueryString();

        Assert.Contains("\"Id\" <", sql);
    }

    public void Dispose() => _dbContext.Dispose();
}
