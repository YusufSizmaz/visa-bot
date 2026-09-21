using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VisaTelegramBot.Application.Abstractions.Locking;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.Abstractions.Scraping;
using VisaTelegramBot.Application.Common;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsItems;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Application.NewsSources.Fetch;

/// <summary>
/// Bir kaynagi okur, yeni haberleri ayiklar ve kaydeder. Telegram'a GONDERMEZ.
/// Kaydetme ve gonderme bilincli olarak ayrilmistir (Outbox): kayit basarili olup gonderim coktugunde haber kaybolmaz.
/// </summary>
internal sealed class FetchNewsSourceCommandHandler(
    INewsSourceRepository newsSourceRepository,
    INewsItemRepository newsItemRepository,
    IUnitOfWork unitOfWork,
    INewsFeedReaderResolver feedReaderResolver,
    IDistributedLockProvider lockProvider,
    IOptions<NewsFetchingOptions> options,
    TimeProvider timeProvider,
    ILogger<FetchNewsSourceCommandHandler> logger) : ICommandHandler<FetchNewsSourceCommand, FetchNewsSourceResult>
{
    /// <summary>Tek beslemeden islenecek mutlak ust sinir. Bozuk veya kotu niyetli bir kaynaga karsi emniyet.</summary>
    private const int MaxEntriesPerFetch = 1000;

    public async Task<Result<FetchNewsSourceResult>> Handle(
        FetchNewsSourceCommand command,
        CancellationToken cancellationToken)
    {
        var id = command.NewsSourceId;

        // Birden fazla worker kopyasi varsa ayni kaynagi ayni anda sadece biri ceker.
        await using var fetchLock = await lockProvider.TryAcquireAsync($"news-source-fetch:{id:N}", cancellationToken);

        if (fetchLock is null)
        {
            return FetchNewsSourceResult.Skipped(id, "Kaynak şu anda başka bir işlem tarafından çekiliyor.");
        }

        var source = await newsSourceRepository.GetByIdAsync(id, cancellationToken);

        if (source is null)
        {
            return NewsSourceErrors.NotFound(id);
        }

        var startedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        if (!source.IsActive)
        {
            return FetchNewsSourceResult.Skipped(id, "Kaynak pasif.");
        }

        if (!command.Force && !source.IsDueForFetch(startedAtUtc))
        {
            return FetchNewsSourceResult.Skipped(id, "Çekim zamanı henüz gelmedi.");
        }

        IReadOnlyList<FeedEntry> entries;

        try
        {
            entries = await feedReaderResolver.Resolve(source.Type).ReadAsync(source, cancellationToken);
        }
        catch (FeedReadException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Kaynak {NewsSourceName} ({NewsSourceId}) okunamadı.", source.Name, source.Id);

            source.RecordFetchFailure(exception.Message, startedAtUtc);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return NewsSourceErrors.FetchFailed(id, exception.Message);
        }

        var candidates = BuildCandidates(entries);

        var existingHashes = await newsItemRepository.GetExistingContentHashesAsync(
            candidates.Select(candidate => candidate.ContentHash).ToArray(),
            cancellationToken);

        var newItems = CreateNewsItems(source, candidates, existingHashes, startedAtUtc);

        newsItemRepository.AddRange(newItems);
        source.RecordFetchSuccess(startedAtUtc);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException exception)
        {
            // Iki farkli kaynak ayni haberi ayni anda buldu. Unique index cift kaydi engelledi.
            // Kaynak basarili sayilmadi; bir sonraki cekimde tekrar denenecek ve bu kez ayiklanacak.
            logger.LogInformation(exception, "Kaynak {NewsSourceId} için eş zamanlı kayıt çakışması.", id);
            return NewsSourceErrors.ConcurrentFetch;
        }

        var queued = newItems.Count(item => item.DeliveryStatus == DeliveryStatus.Pending);

        logger.LogInformation(
            "Kaynak {NewsSourceName} çekildi: {EntriesRead} kayıt okundu, {NewItems} yeni, {Queued} kuyrukta, {Archived} arşivlendi.",
            source.Name,
            entries.Count,
            newItems.Count,
            queued,
            newItems.Count - queued);

        return new FetchNewsSourceResult(
            id,
            FetchStatus.Completed,
            SkipReason: null,
            EntriesRead: entries.Count,
            NewItems: newItems.Count,
            QueuedForDelivery: queued,
            Archived: newItems.Count - queued);
    }

    private List<Candidate> BuildCandidates(IReadOnlyList<FeedEntry> entries)
    {
        var candidates = new List<Candidate>();
        var seenHashes = new HashSet<ContentHash>();

        foreach (var entry in entries)
        {
            // Tum kayitlar parmak izi kontrolune girer. Aksi halde sinirin disinda kalan eski kayitlar
            // bir sonraki cekimde "yeni" sanilip kanala gonderilirdi.
            if (candidates.Count >= MaxEntriesPerFetch)
            {
                break;
            }

            var title = NewsItem.NormalizeText(entry.Title);

            if (title.Length == 0 || !WebUrl.TryCreate(entry.Link, out var url, out _))
            {
                logger.LogDebug("Başlığı veya geçerli linki olmayan kayıt atlandı: {Title} {Link}", entry.Title, entry.Link);
                continue;
            }

            var contentHash = ContentHash.FromUrl(url);

            // Ayni beslemede ayni link iki kez gelebilir.
            if (!seenHashes.Add(contentHash))
            {
                continue;
            }

            var summary = NewsItem.NormalizeText(entry.Summary);

            candidates.Add(new Candidate(
                candidates.Count,
                TextTruncation.Truncate(title, NewsItem.TitleMaxLength),
                url,
                contentHash,
                summary.Length == 0 || summary == title ? null : TextTruncation.Truncate(summary, NewsItem.SummaryMaxLength),
                entry.PublishedAtUtc));
        }

        return candidates;
    }

    private List<NewsItem> CreateNewsItems(
        NewsSource source,
        IReadOnlyList<Candidate> candidates,
        IReadOnlySet<string> existingHashes,
        DateTime utcNow)
    {
        // Ilk basarili cekimde her sey arsivlenir: yeni eklenen kaynak kanala eski haber dokmez.
        var archiveAll = source.HasNeverSucceeded;
        var oldestDeliverableUtc = utcNow - options.Value.MaxItemAge;

        var newCandidates = candidates
            .Where(candidate => !existingHashes.Contains(candidate.ContentHash.Value))
            .ToList();

        // Her aday icin once "neden gonderilmemeli?" sorusu cevaplanir. Sira onemli: panelde en anlamli neden gorunsun.
        var archiveReasons = newCandidates.ToDictionary(
            candidate => candidate.FeedIndex,
            candidate => archiveAll ? ArchiveReason.InitialImport
                : candidate.PublishedAtUtc < oldestDeliverableUtc ? ArchiveReason.TooOld
                : !source.IsRelevant(candidate.Title, candidate.Summary) ? ArchiveReason.NotRelevant
                : !source.IsLanguageAllowed(candidate.Title, candidate.Summary) ? ArchiveReason.NotTurkish
                : (ArchiveReason?)null);

        // Kuyruga en fazla N haber girer. Beslemeler genelde en yeniden eskiye sirali oldugu icin
        // en yeni haberler secilir; tarihi olmayanlar besleme sirasina gore degerlendirilir.
        var queueable = newCandidates
            .Where(candidate => archiveReasons[candidate.FeedIndex] is null)
            .OrderByDescending(candidate => candidate.PublishedAtUtc ?? DateTime.MinValue)
            .ThenBy(candidate => candidate.FeedIndex)
            .Take(options.Value.MaxQueuedItemsPerFetch)
            .Select(candidate => candidate.FeedIndex)
            .ToHashSet();

        // Kanalda dogal akis icin eskiden yeniye kaydederiz.
        var ordered = newCandidates
            .OrderBy(candidate => candidate.PublishedAtUtc ?? DateTime.MaxValue)
            .ThenByDescending(candidate => candidate.FeedIndex)
            .ToList();

        var items = new List<NewsItem>(ordered.Count);

        for (var index = 0; index < ordered.Count; index++)
        {
            var candidate = ordered[index];

            // Ayni cekimdeki haberlere birbirinden farkli ve sirali bir kesif zamani verilir.
            // Gonderim sirasi ve cursor sayfalama bu zamana dayanir.
            var discoveredAtUtc = utcNow.AddTicks(index);

            if (queueable.Contains(candidate.FeedIndex))
            {
                items.Add(NewsItem.Discover(source.Id, candidate.Title, candidate.Url, candidate.Summary, candidate.PublishedAtUtc, discoveredAtUtc));
                continue;
            }

            var reason = archiveReasons[candidate.FeedIndex] ?? ArchiveReason.QueueLimit;

            items.Add(NewsItem.Archive(source.Id, candidate.Title, candidate.Url, candidate.Summary, candidate.PublishedAtUtc, discoveredAtUtc, reason));
        }

        return items;
    }

    private sealed record Candidate(
        int FeedIndex,
        string Title,
        WebUrl Url,
        ContentHash ContentHash,
        string? Summary,
        DateTime? PublishedAtUtc);
}
