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
            .UseSqlServer("Server=localhost;Database=ModelOnly;Integrated Security=true;TrustServerCertificate=True")
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
    public void NewsItems_ClusteredIndexIsTimeOrdered_NotGuid()
    {
        // Index ayrintilari calisma zamani modelinde tutulmaz; migration'larin kullandigi tasarim modeline bakariz.
        var entity = _dbContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(NewsItem))!;

        Assert.False(entity.FindPrimaryKey()!.IsClustered());
        Assert.Contains(entity.GetIndexes(), index => index.IsClustered() == true && index.IsUnique);
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

        Assert.Contains("[Id] <", sql);
    }

    public void Dispose() => _dbContext.Dispose();
}
