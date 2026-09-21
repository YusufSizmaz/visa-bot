using System.Globalization;
using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Domain.FlightDeals;

public enum FlightDealStatus
{
    /// <summary>Bulundu, yoneticinin kararini bekliyor.</summary>
    New = 1,

    /// <summary>Kanal mesajina donusturuldu.</summary>
    Published = 2,

    /// <summary>Yonetici gondermemeye karar verdi.</summary>
    Dismissed = 3
}

/// <summary>
/// Bir rotada azami fiyatin altinda bulunan bilet. Aggregate root.
/// Ayni ucus ayni fiyatla tekrar bulunursa DealKey sayesinde ikinci kez kaydedilmez.
/// </summary>
public sealed class FlightDeal : AggregateRoot<Guid>
{
    public const int AirlineMaxLength = 10;
    public const int FlightNumberMaxLength = 20;
    public const int DealKeyMaxLength = 120;

    private FlightDeal()
    {
    }

    private FlightDeal(Guid id) : base(id)
    {
    }

    public Guid FlightRouteId { get; private set; }

    public AirportCode Origin { get; private set; } = null!;

    public AirportCode Destination { get; private set; } = null!;

    public DateTimeOffset DepartureAt { get; private set; }

    public decimal Price { get; private set; }

    public string Currency { get; private set; } = null!;

    public string? Airline { get; private set; }

    public string? FlightNumber { get; private set; }

    public int Transfers { get; private set; }

    public WebUrl BookingUrl { get; private set; } = null!;

    /// <summary>Tekrar onleme anahtari: rota + kalkis zamani + havayolu + ucus no + fiyat.</summary>
    public string DealKey { get; private set; } = null!;

    public DateTime FoundAtUtc { get; private set; }

    public FlightDealStatus Status { get; private set; }

    public Guid? ChannelMessageId { get; private set; }

    public static FlightDeal Found(
        Guid flightRouteId,
        AirportCode origin,
        AirportCode destination,
        DateTimeOffset departureAt,
        decimal price,
        string currency,
        string? airline,
        string? flightNumber,
        int transfers,
        WebUrl bookingUrl,
        DateTime utcNow)
    {
        if (flightRouteId == Guid.Empty)
        {
            throw new DomainException("Fırsatın rotası belirtilmeli.");
        }

        if (price <= 0)
        {
            throw new DomainException("Bilet fiyatı sıfırdan büyük olmalı.");
        }

        var normalizedAirline = Clean(airline, AirlineMaxLength);
        var normalizedFlightNumber = Clean(flightNumber, FlightNumberMaxLength);

        return new FlightDeal(Guid.CreateVersion7())
        {
            FlightRouteId = flightRouteId,
            Origin = origin,
            Destination = destination,
            DepartureAt = departureAt,
            Price = decimal.Round(price, 2),
            Currency = string.IsNullOrWhiteSpace(currency) ? "TRY" : currency.Trim().ToUpperInvariant(),
            Airline = normalizedAirline,
            FlightNumber = normalizedFlightNumber,
            Transfers = Math.Max(0, transfers),
            BookingUrl = bookingUrl,
            DealKey = BuildKey(flightRouteId, departureAt, normalizedAirline, normalizedFlightNumber, price),
            FoundAtUtc = utcNow,
            Status = FlightDealStatus.New
        };
    }

    public static string BuildKey(Guid routeId, DateTimeOffset departureAt, string? airline, string? flightNumber, decimal price)
    {
        return string.Join(
            '|',
            routeId.ToString("N"),
            departureAt.UtcDateTime.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture),
            airline ?? "-",
            flightNumber ?? "-",
            decimal.Round(price, 0).ToString(CultureInfo.InvariantCulture));
    }

    public void MarkAsPublished(Guid channelMessageId)
    {
        if (Status == FlightDealStatus.Published)
        {
            return;
        }

        if (Status != FlightDealStatus.New)
        {
            throw new DomainException("Sadece yeni fırsatlar kanala gönderilebilir.");
        }

        Status = FlightDealStatus.Published;
        ChannelMessageId = channelMessageId;
    }

    public void Dismiss()
    {
        if (Status == FlightDealStatus.Published)
        {
            throw new DomainException("Kanala gönderilmiş bir fırsat reddedilemez.");
        }

        Status = FlightDealStatus.Dismissed;
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
