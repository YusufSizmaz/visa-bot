using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsSources.Events;

namespace VisaTelegramBot.Domain.NewsSources;

/// <summary>
/// Haberlerin cekildigi kaynak. Aggregate root.
/// </summary>
public sealed class NewsSource : AggregateRoot<Guid>
{
    public const int NameMaxLength = 100;
    public const int LastFetchErrorMaxLength = 1000;
    public const int MaxConsecutiveFailures = 5;
    public static readonly TimeSpan MinFetchInterval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan MaxFetchInterval = TimeSpan.FromHours(24);

    // EF Core veritabanindan okurken bu constructor'i kullanir.
    private NewsSource()
    {
    }

    private NewsSource(
        Guid id,
        string name,
        WebUrl url,
        SourceType type,
        HtmlParsingRules? parsingRules,
        TimeSpan fetchInterval,
        DateTime utcNow) : base(id)
    {
        Name = name;
        Url = url;
        Type = type;
        ParsingRules = parsingRules;
        FetchInterval = fetchInterval;
        IsActive = true;
        CreatedAtUtc = utcNow;
        NextFetchAtUtc = utcNow;
    }

    public string Name { get; private set; } = null!;

    public WebUrl Url { get; private set; } = null!;

    public SourceType Type { get; private set; }

    public HtmlParsingRules? ParsingRules { get; private set; }

    /// <summary>Tanimliysa sadece eslesen haberler kanala gonderilir; digerleri kaydedilip arsivlenir.</summary>
    public KeywordFilter? KeywordFilter { get; private set; }

    /// <summary>True ise sadece Turkce basligi veya ozeti olan haberler kanala gonderilir.</summary>
    public bool TurkishOnly { get; private set; }

    public SourceCategory Category { get; private set; } = SourceCategory.Visa;

    public TimeSpan FetchInterval { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Bir sonraki cekimin zamani. "Son cekim + aralik" hesabini her sorguda SQL'de yapmak yerine
    /// saklanir; boylece indekslenebilir ve cok sayida kaynakta sorgu hizli kalir.
    /// </summary>
    public DateTime NextFetchAtUtc { get; private set; }

    public DateTime? LastFetchedAtUtc { get; private set; }

    public DateTime? LastSucceededAtUtc { get; private set; }

    public int ConsecutiveFailureCount { get; private set; }

    public string? LastFetchError { get; private set; }

    /// <summary>
    /// Kaynak hic basariyla cekilmedi mi? Ilk cekimdeki haberler kanala basilmaz, arsivlenir.
    /// Aksi halde yeni eklenen bir kaynak kanala onlarca eski haber doker.
    /// </summary>
    public bool HasNeverSucceeded => LastSucceededAtUtc is null;

    public static NewsSource Create(
        string name,
        WebUrl url,
        SourceType type,
        HtmlParsingRules? parsingRules,
        TimeSpan fetchInterval,
        DateTime utcNow,
        KeywordFilter? keywordFilter = null,
        bool turkishOnly = false,
        SourceCategory category = SourceCategory.Visa)
    {
        ArgumentNullException.ThrowIfNull(url);
        EnsureEndpointIsConsistent(type, parsingRules);
        EnsureValidCategory(category);

        return new NewsSource(
            Guid.CreateVersion7(),
            NormalizeName(name),
            url,
            type,
            parsingRules,
            EnsureValidFetchInterval(fetchInterval),
            utcNow)
        {
            KeywordFilter = keywordFilter,
            TurkishOnly = turkishOnly,
            Category = category
        };
    }

    public void ChangeCategory(SourceCategory category)
    {
        EnsureValidCategory(category);
        Category = category;
    }

    private static void EnsureValidCategory(SourceCategory category)
    {
        if (!Enum.IsDefined(category))
        {
            throw new DomainException("Geçersiz kaynak kategorisi.");
        }
    }

    public void ChangeKeywordFilter(KeywordFilter? keywordFilter)
    {
        KeywordFilter = keywordFilter;
    }

    public void ChangeLanguagePolicy(bool turkishOnly)
    {
        TurkishOnly = turkishOnly;
    }

    /// <summary>Haber bu kaynagin filtresine uyuyor mu? Filtre yoksa her haber uyar.</summary>
    public bool IsRelevant(string title, string? summary) =>
        KeywordFilter is null || KeywordFilter.Matches(title, summary);

    /// <summary>Dil kurali haberin gonderilmesine izin veriyor mu?</summary>
    public bool IsLanguageAllowed(string title, string? summary) =>
        !TurkishOnly || TurkishTextDetector.IsTurkish(title, summary);

    public void Rename(string name)
    {
        Name = NormalizeName(name);
    }

    public void ChangeEndpoint(WebUrl url, SourceType type, HtmlParsingRules? parsingRules)
    {
        ArgumentNullException.ThrowIfNull(url);
        EnsureEndpointIsConsistent(type, parsingRules);

        Url = url;
        Type = type;
        ParsingRules = parsingRules;
    }

    public void ChangeFetchInterval(TimeSpan fetchInterval)
    {
        FetchInterval = EnsureValidFetchInterval(fetchInterval);

        if (LastFetchedAtUtc is not null)
        {
            NextFetchAtUtc = LastFetchedAtUtc.Value + FetchInterval;
        }
    }

    public void Activate(DateTime utcNow)
    {
        // Idempotent: zaten aktifse hicbir sey degismez.
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        ConsecutiveFailureCount = 0;
        LastFetchError = null;
        NextFetchAtUtc = utcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public bool IsDueForFetch(DateTime utcNow) => IsActive && NextFetchAtUtc <= utcNow;

    public void RecordFetchSuccess(DateTime utcNow)
    {
        LastFetchedAtUtc = utcNow;
        LastSucceededAtUtc = utcNow;
        NextFetchAtUtc = utcNow + FetchInterval;
        ConsecutiveFailureCount = 0;
        LastFetchError = null;
    }

    public void RecordFetchFailure(string error, DateTime utcNow)
    {
        // Pasif kaynakta sayac artmaz ve event ikinci kez yayinlanmaz.
        if (!IsActive)
        {
            return;
        }

        LastFetchedAtUtc = utcNow;
        NextFetchAtUtc = utcNow + FetchInterval;
        ConsecutiveFailureCount++;
        LastFetchError = Truncate(error, LastFetchErrorMaxLength);

        if (ConsecutiveFailureCount < MaxConsecutiveFailures)
        {
            return;
        }

        IsActive = false;

        RaiseDomainEvent(new NewsSourceDeactivatedDomainEvent(
            Id,
            Name,
            ConsecutiveFailureCount,
            LastFetchError,
            utcNow));
    }

    private static void EnsureEndpointIsConsistent(SourceType type, HtmlParsingRules? parsingRules)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainException("Geçersiz kaynak tipi.");
        }

        if (type == SourceType.Html && parsingRules is null)
        {
            throw new DomainException("HTML kaynakları için ayrıştırma kuralları zorunludur.");
        }

        if (type == SourceType.Rss && parsingRules is not null)
        {
            throw new DomainException("RSS kaynakları ayrıştırma kuralı almaz.");
        }
    }

    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Kaynak adı boş olamaz.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainException($"Kaynak adı en fazla {NameMaxLength} karakter olabilir.");
        }

        return trimmed;
    }

    private static TimeSpan EnsureValidFetchInterval(TimeSpan interval)
    {
        if (interval < MinFetchInterval || interval > MaxFetchInterval)
        {
            throw new DomainException(
                $"Çekim aralığı {MinFetchInterval} ile {MaxFetchInterval} arasında olmalı.");
        }

        return interval;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
