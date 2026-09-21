using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VisaTelegramBot.Application.Abstractions.Locking;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.FlightDeals;

namespace VisaTelegramBot.Application.FlightDeals;

public sealed record CheckFlightRouteCommand(Guid FlightRouteId, bool Force = false) : ICommand<CheckFlightRouteResult>;

public sealed record CheckFlightRouteResult(Guid FlightRouteId, bool Skipped, int OffersRead, int NewDeals, int AutoPublished);

/// <summary>
/// Bir rotanin onumuzdeki aylardaki en ucuz biletlerini ceker, azami fiyatin altindakileri firsat olarak kaydeder.
/// Rota otomatik yayinliysa firsati ayni islemde kanal mesajina donusturur.
/// </summary>
internal sealed class CheckFlightRouteCommandHandler(
    IFlightRouteRepository routeRepository,
    IFlightDealRepository dealRepository,
    IChannelMessageRepository messageRepository,
    IFlightPriceProvider priceProvider,
    IDistributedLockProvider lockProvider,
    IUnitOfWork unitOfWork,
    IOptions<FlightDealOptions> options,
    TimeProvider timeProvider,
    ILogger<CheckFlightRouteCommandHandler> logger) : ICommandHandler<CheckFlightRouteCommand, CheckFlightRouteResult>
{
    public async Task<Result<CheckFlightRouteResult>> Handle(CheckFlightRouteCommand command, CancellationToken cancellationToken)
    {
        var id = command.FlightRouteId;

        await using var routeLock = await lockProvider.TryAcquireAsync($"flight-route-check:{id:N}", cancellationToken);

        if (routeLock is null)
        {
            return new CheckFlightRouteResult(id, Skipped: true, 0, 0, 0);
        }

        var route = await routeRepository.GetByIdAsync(id, cancellationToken);

        if (route is null)
        {
            return FlightDealErrors.RouteNotFound(id);
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        if (!route.IsActive || (!command.Force && !route.IsDueForCheck(utcNow)))
        {
            return new CheckFlightRouteResult(id, Skipped: true, 0, 0, 0);
        }

        var offers = new List<FlightOffer>();

        try
        {
            foreach (var month in route.MonthsToSearch(utcNow))
            {
                offers.AddRange(await priceProvider.GetCheapestAsync(route.Origin, route.Destination, month, cancellationToken));
            }
        }
        catch (FlightPriceException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Rota {Route} fiyatları alınamadı.", route.Label);

            route.RecordCheckFailure(exception.Message, utcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return FlightDealErrors.ProviderFailed(exception.Message);
        }

        var candidates = offers
            .Where(offer => offer.Price <= route.MaxPrice && offer.DepartureAt.UtcDateTime > utcNow)
            .Where(offer => WebUrl.TryCreate(offer.BookingUrl, out _, out _))
            .OrderBy(offer => offer.Price)
            .ThenBy(offer => offer.DepartureAt)
            .Select(offer => FlightDeal.Found(
                route.Id,
                route.Origin,
                route.Destination,
                offer.DepartureAt,
                offer.Price,
                offer.Currency,
                offer.Airline,
                offer.FlightNumber,
                offer.Transfers,
                WebUrl.Create(offer.BookingUrl),
                utcNow))
            .DistinctBy(deal => deal.DealKey)
            .ToList();

        var existingKeys = await dealRepository.GetExistingKeysAsync(candidates.Select(deal => deal.DealKey).ToArray(), cancellationToken);

        var newDeals = candidates
            .Where(deal => !existingKeys.Contains(deal.DealKey))
            .Take(options.Value.MaxDealsPerCheck)
            .ToList();

        dealRepository.AddRange(newDeals);

        var autoPublished = 0;

        if (route.AutoPublish)
        {
            foreach (var deal in newDeals)
            {
                var message = FlightDealMessageFactory.Create(deal, route, utcNow);
                messageRepository.Add(message);
                deal.MarkAsPublished(message.Id);
                autoPublished++;
            }
        }

        route.RecordCheckSuccess(utcNow);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException exception)
        {
            logger.LogInformation(exception, "Rota {Route} için eş zamanlı fırsat kaydı; bir sonraki kontrolde ayıklanacak.", route.Label);
            return new CheckFlightRouteResult(id, Skipped: true, offers.Count, 0, 0);
        }

        logger.LogInformation(
            "Rota {Route} kontrol edildi: {Offers} teklif, {NewDeals} yeni fırsat, {AutoPublished} otomatik yayın.",
            route.Label, offers.Count, newDeals.Count, autoPublished);

        return new CheckFlightRouteResult(id, Skipped: false, offers.Count, newDeals.Count, autoPublished);
    }
}
