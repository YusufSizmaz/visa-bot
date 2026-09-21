using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Domain.FlightDeals;

/// <summary>
/// Fiyati takip edilen ucus rotasi. Aggregate root.
/// Belirlenen fiyatin altinda bilet bulunursa bir firsat (FlightDeal) olusur.
/// </summary>
public sealed class FlightRoute : AggregateRoot<Guid>
{
    public const int LabelMaxLength = 100;
    public const int LastErrorMaxLength = 1000;
    public const int MaxMonthsAhead = 12;
    public static readonly TimeSpan MinCheckInterval = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan MaxCheckInterval = TimeSpan.FromDays(1);

    private FlightRoute()
    {
    }

    private FlightRoute(Guid id, DateTime utcNow) : base(id)
    {
        CreatedAtUtc = utcNow;
        NextCheckAtUtc = utcNow;
        IsActive = true;
    }

    public AirportCode Origin { get; private set; } = null!;

    public AirportCode Destination { get; private set; } = null!;

    /// <summary>Panelde ve mesajlarda gorunen ad. Ornek: "İstanbul → Madrid".</summary>
    public string Label { get; private set; } = null!;

    /// <summary>Bu fiyatin (TL) ustundeki biletler firsat sayilmaz.</summary>
    public decimal MaxPrice { get; private set; }

    /// <summary>Bugunden itibaren kac ayin kalkislari aranir.</summary>
    public int MonthsAhead { get; private set; }

    public TimeSpan CheckInterval { get; private set; }

    /// <summary>True ise bulunan firsat onay beklemeden kanala gonderilir.</summary>
    public bool AutoPublish { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime NextCheckAtUtc { get; private set; }

    public DateTime? LastCheckedAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public static FlightRoute Create(
        AirportCode origin,
        AirportCode destination,
        string? label,
        decimal maxPrice,
        int monthsAhead,
        TimeSpan checkInterval,
        bool autoPublish,
        DateTime utcNow)
    {
        var route = new FlightRoute(Guid.CreateVersion7(), utcNow);
        route.Update(origin, destination, label, maxPrice, monthsAhead, checkInterval, autoPublish);

        return route;
    }

    public void Update(
        AirportCode origin,
        AirportCode destination,
        string? label,
        decimal maxPrice,
        int monthsAhead,
        TimeSpan checkInterval,
        bool autoPublish)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);

        if (origin == destination)
        {
            throw new DomainException("Kalkış ve varış aynı olamaz.");
        }

        if (maxPrice <= 0)
        {
            throw new DomainException("Azami fiyat sıfırdan büyük olmalı.");
        }

        if (monthsAhead is < 1 or > MaxMonthsAhead)
        {
            throw new DomainException($"Arama süresi 1 ile {MaxMonthsAhead} ay arasında olmalı.");
        }

        if (checkInterval < MinCheckInterval || checkInterval > MaxCheckInterval)
        {
            throw new DomainException("Kontrol aralığı 30 dakika ile 24 saat arasında olmalı.");
        }

        var normalizedLabel = string.IsNullOrWhiteSpace(label) ? $"{origin} → {destination}" : label.Trim();

        if (normalizedLabel.Length > LabelMaxLength)
        {
            throw new DomainException($"Rota adı en fazla {LabelMaxLength} karakter olabilir.");
        }

        Origin = origin;
        Destination = destination;
        Label = normalizedLabel;
        MaxPrice = decimal.Round(maxPrice, 2);
        MonthsAhead = monthsAhead;
        CheckInterval = checkInterval;
        AutoPublish = autoPublish;
    }

    public void Activate(DateTime utcNow)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        NextCheckAtUtc = utcNow;
        LastError = null;
    }

    public void Deactivate() => IsActive = false;

    public bool IsDueForCheck(DateTime utcNow) => IsActive && NextCheckAtUtc <= utcNow;

    public void RecordCheckSuccess(DateTime utcNow)
    {
        LastCheckedAtUtc = utcNow;
        NextCheckAtUtc = utcNow + CheckInterval;
        LastError = null;
    }

    public void RecordCheckFailure(string error, DateTime utcNow)
    {
        LastCheckedAtUtc = utcNow;
        NextCheckAtUtc = utcNow + CheckInterval;
        LastError = error.Length <= LastErrorMaxLength ? error : error[..LastErrorMaxLength];
    }

    /// <summary>Aranacak kalkis aylari: bu ay ve sonraki (MonthsAhead - 1) ay.</summary>
    public IReadOnlyList<DateOnly> MonthsToSearch(DateTime utcNow)
    {
        var first = new DateOnly(utcNow.Year, utcNow.Month, 1);

        return Enumerable.Range(0, MonthsAhead).Select(first.AddMonths).ToList();
    }
}
