using System.Text.RegularExpressions;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsItems.Events;

namespace VisaTelegramBot.Domain.NewsItems;

/// <summary>
/// Bir kaynaktan cekilmis tek bir haber. Aggregate root.
/// Teslimat durumu da bu aggregate'in parcasidir: tablo ayni zamanda Outbox gorevi gorur.
/// </summary>
public sealed partial class NewsItem : AggregateRoot<Guid>
{
    public const int TitleMaxLength = 500;
    public const int SummaryMaxLength = 2000;
    public const int ExternalMessageIdMaxLength = 100;
    public const int LastDeliveryErrorMaxLength = 1000;
    public const int MaxDeliveryAttempts = 5;
    public static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromHours(1);

    private NewsItem()
    {
    }

    private NewsItem(
        Guid id,
        Guid newsSourceId,
        string title,
        WebUrl url,
        string? summary,
        DateTime? publishedAtUtc,
        DeliveryStatus status,
        DateTime utcNow) : base(id)
    {
        NewsSourceId = newsSourceId;
        Title = title;
        Url = url;
        ContentHash = ContentHash.FromUrl(url);
        Summary = summary;
        PublishedAtUtc = publishedAtUtc;
        DiscoveredAtUtc = utcNow;
        DeliveryStatus = status;
        NextDeliveryAttemptAtUtc = utcNow;
    }

    /// <summary>Kaynaga nesne ile degil kimlik ile baglanir: aggregate'ler arasi referans kurali.</summary>
    public Guid NewsSourceId { get; private set; }

    public string Title { get; private set; } = null!;

    public WebUrl Url { get; private set; } = null!;

    public ContentHash ContentHash { get; private set; } = null!;

    public string? Summary { get; private set; }

    public DateTime? PublishedAtUtc { get; private set; }

    public DateTime DiscoveredAtUtc { get; private set; }

    public DeliveryStatus DeliveryStatus { get; private set; }

    public int DeliveryAttempts { get; private set; }

    public DateTime NextDeliveryAttemptAtUtc { get; private set; }

    /// <summary>
    /// Birden fazla worker ayni haberi ayni anda gondermesin diye kullanilan kiralama suresi.
    /// Veritabaninda atomik bir UPDATE ile doldurulur, teslimat bitince temizlenir.
    /// </summary>
    public DateTime? DeliveryLockedUntilUtc { get; private set; }

    public DateTime? DeliveredAtUtc { get; private set; }

    /// <summary>Gonderim kanalinin mesaja verdigi kimlik. Domain Telegram'i bilmez, bu yuzden genel bir isim.</summary>
    public string? ExternalMessageId { get; private set; }

    public string? LastDeliveryError { get; private set; }

    /// <summary>Arsivlenen haberin neden gonderilmedigi. Diger durumlarda null.</summary>
    public ArchiveReason? ArchiveReason { get; private set; }

    /// <summary>Yeni haber: kanala gonderilmek uzere kuyruga girer.</summary>
    public static NewsItem Discover(
        Guid newsSourceId,
        string title,
        WebUrl url,
        string? summary,
        DateTime? publishedAtUtc,
        DateTime utcNow)
    {
        var item = Create(newsSourceId, title, url, summary, publishedAtUtc, DeliveryStatus.Pending, utcNow);

        item.RaiseDomainEvent(new NewsItemDiscoveredDomainEvent(item.Id, newsSourceId, item.Title, utcNow));

        return item;
    }

    /// <summary>Kaydedilir ama gonderilmez. Tekrar gorulurse yeni haber sanilmasin diye saklanir.</summary>
    public static NewsItem Archive(
        Guid newsSourceId,
        string title,
        WebUrl url,
        string? summary,
        DateTime? publishedAtUtc,
        DateTime utcNow,
        ArchiveReason reason = NewsItems.ArchiveReason.InitialImport)
    {
        var item = Create(newsSourceId, title, url, summary, publishedAtUtc, DeliveryStatus.Archived, utcNow);
        item.ArchiveReason = reason;

        return item;
    }

    public bool IsDeliverable(DateTime utcNow) =>
        DeliveryStatus == DeliveryStatus.Pending && NextDeliveryAttemptAtUtc <= utcNow;

    public void MarkAsDelivered(string? externalMessageId, DateTime utcNow)
    {
        // Idempotent: ayni teslimat iki kez bildirilirse ikinci cagri etkisizdir.
        if (DeliveryStatus == DeliveryStatus.Delivered)
        {
            return;
        }

        EnsurePending();

        DeliveryStatus = DeliveryStatus.Delivered;
        DeliveryAttempts++;
        DeliveredAtUtc = utcNow;
        ExternalMessageId = externalMessageId is null ? null : Truncate(externalMessageId, ExternalMessageIdMaxLength);
        LastDeliveryError = null;
        DeliveryLockedUntilUtc = null;
    }

    /// <summary>
    /// Hiz limiti gibi haberin kendisinden kaynaklanmayan durumlarda teslimati erteler. Deneme sayilmaz.
    /// </summary>
    public void DeferDelivery(TimeSpan delay, DateTime utcNow)
    {
        EnsurePending();

        NextDeliveryAttemptAtUtc = utcNow + (delay < TimeSpan.Zero ? TimeSpan.Zero : delay);
        DeliveryLockedUntilUtc = null;
    }

    public void RecordDeliveryFailure(string error, bool isPermanent, DateTime utcNow)
    {
        if (DeliveryStatus != DeliveryStatus.Pending)
        {
            return;
        }

        DeliveryAttempts++;
        LastDeliveryError = Truncate(error, LastDeliveryErrorMaxLength);
        DeliveryLockedUntilUtc = null;

        if (!isPermanent && DeliveryAttempts < MaxDeliveryAttempts)
        {
            NextDeliveryAttemptAtUtc = utcNow + CalculateRetryDelay(DeliveryAttempts);
            return;
        }

        DeliveryStatus = DeliveryStatus.Failed;

        RaiseDomainEvent(new NewsItemDeliveryFailedDomainEvent(Id, DeliveryAttempts, LastDeliveryError, utcNow));
    }

    /// <summary>Basarisiz veya arsivlenmis haberi elle yeniden kuyruga alir.</summary>
    public void RequeueForDelivery(DateTime utcNow)
    {
        if (DeliveryStatus == DeliveryStatus.Delivered)
        {
            throw new DomainException("Gönderilmiş bir haber yeniden kuyruğa alınamaz.");
        }

        DeliveryStatus = DeliveryStatus.Pending;
        DeliveryAttempts = 0;
        NextDeliveryAttemptAtUtc = utcNow;
        LastDeliveryError = null;
        DeliveryLockedUntilUtc = null;
        ArchiveReason = null;
    }

    /// <summary>
    /// Exponential backoff: 30sn, 1dk, 2dk, 4dk... en fazla 1 saat.
    /// Sorunlu bir servise art arda yuklenmek yerine her denemede daha uzun bekler.
    /// </summary>
    public static TimeSpan CalculateRetryDelay(int attempt)
    {
        if (attempt <= 1)
        {
            return BaseRetryDelay;
        }

        var exponent = Math.Min(attempt - 1, 16);
        var delay = TimeSpan.FromTicks(BaseRetryDelay.Ticks * (1L << exponent));

        return delay > MaxRetryDelay ? MaxRetryDelay : delay;
    }

    /// <summary>Bosluklari tek bosluga indirir ve kirpar. Kaynaklardan gelen metin genelde daginiktir.</summary>
    public static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : WhitespaceRegex().Replace(value, " ").Trim();
    }

    private static NewsItem Create(
        Guid newsSourceId,
        string title,
        WebUrl url,
        string? summary,
        DateTime? publishedAtUtc,
        DeliveryStatus status,
        DateTime utcNow)
    {
        if (newsSourceId == Guid.Empty)
        {
            throw new DomainException("Haberin kaynağı belirtilmeli.");
        }

        ArgumentNullException.ThrowIfNull(url);

        var normalizedTitle = NormalizeText(title);

        if (normalizedTitle.Length == 0)
        {
            throw new DomainException("Haber başlığı boş olamaz.");
        }

        if (normalizedTitle.Length > TitleMaxLength)
        {
            throw new DomainException($"Haber başlığı en fazla {TitleMaxLength} karakter olabilir.");
        }

        var normalizedSummary = NormalizeText(summary);

        if (normalizedSummary.Length > SummaryMaxLength)
        {
            throw new DomainException($"Haber özeti en fazla {SummaryMaxLength} karakter olabilir.");
        }

        return new NewsItem(
            Guid.CreateVersion7(),
            newsSourceId,
            normalizedTitle,
            url,
            normalizedSummary.Length == 0 ? null : normalizedSummary,
            publishedAtUtc,
            status,
            utcNow);
    }

    private void EnsurePending()
    {
        if (DeliveryStatus != DeliveryStatus.Pending)
        {
            throw new DomainException($"Bu işlem sadece bekleyen haberlerde yapılabilir. Mevcut durum: {DeliveryStatus}.");
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
