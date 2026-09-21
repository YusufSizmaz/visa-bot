using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using VisaTelegramBot.Application.Abstractions.Locking;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.FlightDeals;
using VisaTelegramBot.Domain.ChannelMessages;
using VisaTelegramBot.Domain.FlightDeals;

namespace VisaTelegramBot.Application.Tests.FlightDeals;

public sealed class FlightDealHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly IFlightRouteRepository _routes = Substitute.For<IFlightRouteRepository>();
    private readonly IFlightDealRepository _deals = Substitute.For<IFlightDealRepository>();
    private readonly IChannelMessageRepository _messages = Substitute.For<IChannelMessageRepository>();
    private readonly IFlightPriceProvider _prices = Substitute.For<IFlightPriceProvider>();
    private readonly IDistributedLockProvider _locks = Substitute.For<IDistributedLockProvider>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly List<FlightDeal> _addedDeals = [];
    private readonly List<ChannelMessage> _addedMessages = [];

    public FlightDealHandlerTests()
    {
        _locks.TryAcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Substitute.For<IAsyncDisposable>());
        _deals.GetExistingKeysAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(new HashSet<string>());
        _deals.AddRange(Arg.Do<IEnumerable<FlightDeal>>(deals => _addedDeals.AddRange(deals)));
        _messages.Add(Arg.Do<ChannelMessage>(_addedMessages.Add));
        _prices.GetCheapestAsync(Arg.Any<AirportCode>(), Arg.Any<AirportCode>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    [Fact]
    public async Task Check_KeepsOnlyOffersUnderMaxPrice_CheapestFirst_LimitedPerCheck()
    {
        var route = GivenRoute(maxPrice: 3000m, autoPublish: false);

        _prices.GetCheapestAsync(route.Origin, route.Destination, new DateOnly(2026, 9, 1), Arg.Any<CancellationToken>())
            .Returns([Offer(2800m, 20), Offer(3500m, 21), Offer(1900m, 22), Offer(2500m, 23), Offer(2100m, 24)]);

        var result = await CheckHandler().Handle(new CheckFlightRouteCommand(route.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.NewDeals);
        Assert.Equal([1900m, 2100m, 2500m], _addedDeals.Select(deal => deal.Price));
        Assert.Empty(_addedMessages);
    }

    [Fact]
    public async Task Check_SkipsDealsAlreadyStored()
    {
        var route = GivenRoute(maxPrice: 3000m, autoPublish: false);
        var offer = Offer(1900m, 22);
        var knownKey = FlightDeal.BuildKey(route.Id, offer.DepartureAt, offer.Airline, offer.FlightNumber, offer.Price);

        _deals.GetExistingKeysAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(new HashSet<string> { knownKey });
        _prices.GetCheapestAsync(route.Origin, route.Destination, new DateOnly(2026, 9, 1), Arg.Any<CancellationToken>()).Returns([offer]);

        var result = await CheckHandler().Handle(new CheckFlightRouteCommand(route.Id), CancellationToken.None);

        Assert.Equal(0, result.Value.NewDeals);
    }

    [Fact]
    public async Task Check_AutoPublishRoute_CreatesTurkishChannelMessage()
    {
        var route = GivenRoute(maxPrice: 3000m, autoPublish: true);
        _prices.GetCheapestAsync(route.Origin, route.Destination, new DateOnly(2026, 9, 1), Arg.Any<CancellationToken>()).Returns([Offer(1900m, 22)]);

        var result = await CheckHandler().Handle(new CheckFlightRouteCommand(route.Id), CancellationToken.None);

        Assert.Equal(1, result.Value.AutoPublished);
        var message = Assert.Single(_addedMessages);
        Assert.Equal(ChannelMessageKind.FlightDeal, message.Kind);
        Assert.Contains("1.900 ₺", message.Title);
        Assert.Contains("Gidiş", message.Body);
        Assert.Equal("Bileti incele", message.Button!.Text);
        Assert.Equal(FlightDealStatus.Published, Assert.Single(_addedDeals).Status);
    }

    [Fact]
    public async Task Check_ProviderFailure_RecordsErrorOnRoute()
    {
        var route = GivenRoute(maxPrice: 3000m, autoPublish: false);
        _prices.GetCheapestAsync(Arg.Any<AirportCode>(), Arg.Any<AirportCode>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<FlightOffer>>(_ => throw new FlightPriceException("token geçersiz"));

        var result = await CheckHandler().Handle(new CheckFlightRouteCommand(route.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("token geçersiz", route.LastError);
    }

    private CheckFlightRouteCommandHandler CheckHandler() => new(
        _routes, _deals, _messages, _prices, _locks, _unitOfWork,
        Options.Create(new FlightDealOptions()), new FakeTimeProvider(Now), NullLogger<CheckFlightRouteCommandHandler>.Instance);

    private FlightRoute GivenRoute(decimal maxPrice, bool autoPublish)
    {
        var route = FlightRoute.Create(
            AirportCode.Create("IST"), AirportCode.Create("MAD"), "İstanbul → Madrid", maxPrice, 1, TimeSpan.FromHours(6), autoPublish,
            Now.UtcDateTime.AddDays(-1));

        _routes.GetByIdAsync(route.Id, Arg.Any<CancellationToken>()).Returns(route);
        return route;
    }

    private static FlightOffer Offer(decimal price, int day) => new(
        "IST", "MAD", new DateTimeOffset(2026, 9, day, 6, 55, 0, TimeSpan.FromHours(3)), price, "TRY", "PC", "1234", 0,
        $"https://www.aviasales.com/search/IST{day}09MAD1");
}
