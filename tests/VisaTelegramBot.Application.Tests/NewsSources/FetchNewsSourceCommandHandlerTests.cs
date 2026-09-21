using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using VisaTelegramBot.Application.Abstractions.Locking;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.Abstractions.Scraping;
using VisaTelegramBot.Application.NewsSources.Fetch;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsItems;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Application.Tests.NewsSources;

public sealed class FetchNewsSourceCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly INewsSourceRepository _sources = Substitute.For<INewsSourceRepository>();
    private readonly INewsItemRepository _items = Substitute.For<INewsItemRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly INewsFeedReaderResolver _resolver = Substitute.For<INewsFeedReaderResolver>();
    private readonly INewsFeedReader _reader = Substitute.For<INewsFeedReader>();
    private readonly IDistributedLockProvider _locks = Substitute.For<IDistributedLockProvider>();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly List<NewsItem> _added = [];

    public FetchNewsSourceCommandHandlerTests()
    {
        _resolver.Resolve(Arg.Any<SourceType>()).Returns(_reader);
        _locks.TryAcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Substitute.For<IAsyncDisposable>());
        _items.GetExistingContentHashesAsync(Arg.Any<IReadOnlyCollection<ContentHash>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>());
        _items.AddRange(Arg.Do<IEnumerable<NewsItem>>(items => _added.AddRange(items)));
    }

    [Fact]
    public async Task FirstSuccessfulFetch_ArchivesEverything_SoChannelIsNotFlooded()
    {
        var source = GivenSource();
        GivenEntries(Entry("https://example.com/1"), Entry("https://example.com/2"));

        var result = await CreateHandler().Handle(new FetchNewsSourceCommand(source.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Archived);
        Assert.Equal(0, result.Value.QueuedForDelivery);
        Assert.All(_added, item => Assert.Equal(DeliveryStatus.Archived, item.DeliveryStatus));
        Assert.False(source.HasNeverSucceeded);
    }

    [Fact]
    public async Task LaterFetch_QueuesOnlyNewItems()
    {
        var source = GivenSource(succeededBefore: true);
        var known = ContentHash.FromUrl(WebUrl.Create("https://example.com/known")).Value;

        _items.GetExistingContentHashesAsync(Arg.Any<IReadOnlyCollection<ContentHash>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string> { known });

        GivenEntries(Entry("https://example.com/known"), Entry("https://example.com/new"));

        var result = await CreateHandler().Handle(new FetchNewsSourceCommand(source.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.NewItems);
        Assert.Equal(1, result.Value.QueuedForDelivery);
        Assert.Equal("https://example.com/new", Assert.Single(_added).Url.Value);
    }

    [Fact]
    public async Task EntriesOlderThanMaxAge_AreArchived()
    {
        var source = GivenSource(succeededBefore: true);
        GivenEntries(
            Entry("https://example.com/old", Now.UtcDateTime.AddDays(-30)),
            Entry("https://example.com/fresh", Now.UtcDateTime.AddHours(-1)));

        var result = await CreateHandler().Handle(new FetchNewsSourceCommand(source.Id), CancellationToken.None);

        Assert.Equal(1, result.Value.QueuedForDelivery);
        Assert.Equal(1, result.Value.Archived);
    }

    [Fact]
    public async Task MoreNewItemsThanQueueLimit_NewestAreQueued_RestArchived()
    {
        var source = GivenSource(succeededBefore: true);
        var limit = new NewsFetchingOptions().MaxQueuedItemsPerFetch;

        var entries = Enumerable.Range(0, limit + 5)
            .Select(i => Entry($"https://example.com/{i}", Now.UtcDateTime.AddMinutes(-i)))
            .ToArray();

        GivenEntries(entries);

        var result = await CreateHandler().Handle(new FetchNewsSourceCommand(source.Id), CancellationToken.None);

        Assert.Equal(limit + 5, result.Value.NewItems);
        Assert.Equal(limit, result.Value.QueuedForDelivery);
        Assert.Equal(5, result.Value.Archived);

        // Arsivlenenler en eski bes haber olmali.
        var archivedUrls = _added.Where(item => item.DeliveryStatus == DeliveryStatus.Archived).Select(item => item.Url.Value);
        Assert.All(archivedUrls, url => Assert.True(int.Parse(url.Split('/')[^1]) >= limit));
    }

    [Fact]
    public async Task KeywordFilter_QueuesOnlyRelevantNews()
    {
        var source = GivenSource(succeededBefore: true);
        source.ChangeKeywordFilter(KeywordFilter.CreateOrNull(["randevu", "cita"]));

        GivenEntries(
            new FeedEntry("Vize randevuları açıldı", "https://example.com/randevu", null, Now.UtcDateTime.AddMinutes(-5)),
            new FeedEntry("Nuevas citas disponibles", "https://example.com/cita", null, Now.UtcDateTime.AddMinutes(-4)),
            new FeedEntry("Seçim duyurusu", "https://example.com/secim", null, Now.UtcDateTime.AddMinutes(-3)));

        var result = await CreateHandler().Handle(new FetchNewsSourceCommand(source.Id), CancellationToken.None);

        Assert.Equal(2, result.Value.QueuedForDelivery);
        Assert.Equal(1, result.Value.Archived);
        Assert.Equal(DeliveryStatus.Archived, _added.Single(item => item.Url.Value.EndsWith("secim", StringComparison.Ordinal)).DeliveryStatus);
    }

    [Fact]
    public async Task TurkishOnlySource_ArchivesForeignLanguageNews_WithReason()
    {
        var source = GivenSource(succeededBefore: true);
        source.ChangeLanguagePolicy(turkishOnly: true);

        GivenEntries(
            new FeedEntry("Vize randevuları açıldı", "https://example.com/tr", null, Now.UtcDateTime.AddMinutes(-5)),
            new FeedEntry("New visa appointment platform", "https://example.com/en", null, Now.UtcDateTime.AddMinutes(-4)));

        var result = await CreateHandler().Handle(new FetchNewsSourceCommand(source.Id), CancellationToken.None);

        Assert.Equal(1, result.Value.QueuedForDelivery);
        var english = _added.Single(item => item.Url.Value.EndsWith("/en", StringComparison.Ordinal));
        Assert.Equal(DeliveryStatus.Archived, english.DeliveryStatus);
        Assert.Equal(ArchiveReason.NotTurkish, english.ArchiveReason);
    }

    [Fact]
    public async Task FirstFetch_ArchivesWithInitialImportReason()
    {
        var source = GivenSource();
        GivenEntries(Entry("https://example.com/1"));

        await CreateHandler().Handle(new FetchNewsSourceCommand(source.Id), CancellationToken.None);

        Assert.Equal(ArchiveReason.InitialImport, Assert.Single(_added).ArchiveReason);
    }

    [Fact]
    public async Task InvalidAndDuplicateEntries_AreIgnored()
    {
        var source = GivenSource(succeededBefore: true);
        GivenEntries(
            Entry("https://example.com/a"),
            Entry("https://example.com/a?utm_source=x"),
            new FeedEntry("Linksiz", null, null, null),
            new FeedEntry(null, "https://example.com/b", null, null),
            new FeedEntry("Göreli", "/relative", null, null));

        var result = await CreateHandler().Handle(new FetchNewsSourceCommand(source.Id), CancellationToken.None);

        Assert.Equal(1, result.Value.NewItems);
    }

    [Fact]
    public async Task NewItems_AreStoredOldestFirst_WithIncreasingDiscoveryTime()
    {
        var source = GivenSource(succeededBefore: true);
        GivenEntries(
            Entry("https://example.com/newest", Now.UtcDateTime.AddMinutes(-1)),
            Entry("https://example.com/oldest", Now.UtcDateTime.AddMinutes(-30)));

        await CreateHandler().Handle(new FetchNewsSourceCommand(source.Id), CancellationToken.None);

        Assert.Equal("https://example.com/oldest", _added[0].Url.Value);
        Assert.True(_added[0].DiscoveredAtUtc < _added[1].DiscoveredAtUtc);
    }

    [Fact]
    public async Task ReaderFailure_RecordsFailureOnSource()
    {
        var source = GivenSource(succeededBefore: true);
        _reader.ReadAsync(source, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<FeedEntry>>(_ => throw new FeedReadException("HTTP 503"));

        var result = await CreateHandler().Handle(new FetchNewsSourceCommand(source.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(1, source.ConsecutiveFailureCount);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenLockIsHeldElsewhere_SkipsWithoutReading()
    {
        var source = GivenSource();
        _locks.TryAcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((IAsyncDisposable?)null);

        var result = await CreateHandler().Handle(new FetchNewsSourceCommand(source.Id), CancellationToken.None);

        Assert.Equal(FetchStatus.Skipped, result.Value.Status);
        await _reader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
    }

    [Fact]
    public async Task NotDueSource_IsSkipped_UnlessForced()
    {
        var source = GivenSource(succeededBefore: true);
        GivenEntries(Entry("https://example.com/1"));

        // Kaynak az once cekildi; siradaki cekim 15 dakika sonra.
        source.RecordFetchSuccess(Now.UtcDateTime);

        var notForced = await CreateHandler().Handle(new FetchNewsSourceCommand(source.Id), CancellationToken.None);
        var forced = await CreateHandler().Handle(new FetchNewsSourceCommand(source.Id, Force: true), CancellationToken.None);

        Assert.Equal(FetchStatus.Skipped, notForced.Value.Status);
        Assert.Equal(FetchStatus.Completed, forced.Value.Status);
    }

    private FetchNewsSourceCommandHandler CreateHandler() => new(
        _sources,
        _items,
        _unitOfWork,
        _resolver,
        _locks,
        Options.Create(new NewsFetchingOptions()),
        _time,
        NullLogger<FetchNewsSourceCommandHandler>.Instance);

    private NewsSource GivenSource(bool succeededBefore = false)
    {
        var source = NewsSource.Create(
            "Test", WebUrl.Create("https://example.com/feed"), SourceType.Rss, null, TimeSpan.FromMinutes(15), Now.UtcDateTime.AddDays(-1));

        if (succeededBefore)
        {
            // Bir saat once basariyla cekilmis; 15 dakikalik aralik gectigi icin simdi zamani gelmis durumda.
            source.RecordFetchSuccess(Now.UtcDateTime.AddHours(-1));
        }

        _sources.GetByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        return source;
    }

    private void GivenEntries(params FeedEntry[] entries)
    {
        _reader.ReadAsync(Arg.Any<NewsSource>(), Arg.Any<CancellationToken>()).Returns(entries);
    }

    private static FeedEntry Entry(string url, DateTime? publishedAtUtc = null) =>
        new($"Haber {url}", url, "Özet", publishedAtUtc ?? Now.UtcDateTime.AddHours(-1));
}
