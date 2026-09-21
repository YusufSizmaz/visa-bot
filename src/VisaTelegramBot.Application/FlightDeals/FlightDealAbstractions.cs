using System.ComponentModel.DataAnnotations;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Domain.FlightDeals;

namespace VisaTelegramBot.Application.FlightDeals;

/// <summary>Ucus fiyat saglayicisindan (Travelpayouts) gelen tek teklif. Saglayiciya ozgu alanlar icermez.</summary>
public sealed record FlightOffer(
    string Origin,
    string Destination,
    DateTimeOffset DepartureAt,
    decimal Price,
    string Currency,
    string? Airline,
    string? FlightNumber,
    int Transfers,
    string BookingUrl);

public interface IFlightPriceProvider
{
    /// <summary>Verilen aydaki en ucuz tek yon biletleri fiyata gore sirali doner.</summary>
    /// <exception cref="FlightPriceException">Saglayiciya ulasilamazsa veya yapilandirma eksikse.</exception>
    Task<IReadOnlyList<FlightOffer>> GetCheapestAsync(
        AirportCode origin,
        AirportCode destination,
        DateOnly departureMonth,
        CancellationToken cancellationToken);
}

public sealed class FlightPriceException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class FlightDealOptions
{
    public const string SectionName = "FlightDeals";

    /// <summary>Tek kontrolde bir rota icin kaydedilecek en fazla firsat.</summary>
    [Range(1, 20)]
    public int MaxDealsPerCheck { get; init; } = 3;
}

public interface IFlightRouteRepository
{
    Task<FlightRoute?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetDueIdsAsync(DateTime utcNow, int limit, CancellationToken cancellationToken);

    void Add(FlightRoute route);
}

public interface IFlightDealRepository
{
    Task<FlightDeal?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> GetExistingKeysAsync(IReadOnlyCollection<string> dealKeys, CancellationToken cancellationToken);

    void AddRange(IEnumerable<FlightDeal> deals);
}

public sealed record FlightRouteResponse(
    Guid Id,
    string Origin,
    string Destination,
    string Label,
    decimal MaxPrice,
    int MonthsAhead,
    int CheckIntervalMinutes,
    bool AutoPublish,
    bool IsActive,
    DateTime NextCheckAtUtc,
    DateTime? LastCheckedAtUtc,
    string? LastError);

public sealed record FlightDealResponse(
    Guid Id,
    Guid FlightRouteId,
    string RouteLabel,
    string Origin,
    string Destination,
    DateTimeOffset DepartureAt,
    decimal Price,
    string Currency,
    string? Airline,
    string? FlightNumber,
    int Transfers,
    string BookingUrl,
    DateTime FoundAtUtc,
    FlightDealStatus Status,
    Guid? ChannelMessageId);

public interface IFlightDealQueries
{
    Task<IReadOnlyList<FlightRouteResponse>> ListRoutesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<FlightDealResponse>> ListDealsAsync(FlightDealStatus? status, int limit, CancellationToken cancellationToken);
}

public static class FlightDealErrors
{
    public static Error RouteNotFound(Guid id) =>
        Error.NotFound("FlightRoute.NotFound", $"'{id}' kimlikli rota bulunamadı.");

    public static Error DealNotFound(Guid id) =>
        Error.NotFound("FlightDeal.NotFound", $"'{id}' kimlikli fırsat bulunamadı.");

    public static Error InvalidState(string message) =>
        Error.Validation("FlightDeal.InvalidState", message);

    public static Error ProviderFailed(string message) =>
        Error.ExternalDependency("FlightDeal.ProviderFailed", message);
}
