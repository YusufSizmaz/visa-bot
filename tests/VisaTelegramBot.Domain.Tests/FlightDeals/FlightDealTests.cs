using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.FlightDeals;

namespace VisaTelegramBot.Domain.Tests.FlightDeals;

public sealed class FlightDealTests
{
    private static readonly DateTime Now = TestData.Now;

    [Theory]
    [InlineData("ist", "IST")]
    [InlineData(" mad ", "MAD")]
    public void AirportCode_NormalizesToUppercase(string input, string expected)
    {
        Assert.Equal(expected, AirportCode.Create(input).Value);
    }

    [Theory]
    [InlineData("IS")]
    [InlineData("ISTA")]
    [InlineData("1ST")]
    [InlineData(null)]
    public void AirportCode_RejectsInvalid(string? input)
    {
        Assert.Throws<DomainException>(() => AirportCode.Create(input));
    }

    [Fact]
    public void Route_SameOriginAndDestination_Throws()
    {
        Assert.Throws<DomainException>(() => Route("IST", "IST"));
    }

    [Fact]
    public void Route_DefaultLabel_UsesCodes()
    {
        Assert.Equal("IST → MAD", Route("IST", "MAD").Label);
    }

    [Fact]
    public void Route_MonthsToSearch_StartsFromCurrentMonth()
    {
        var months = Route("IST", "MAD", monthsAhead: 3).MonthsToSearch(new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal([new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1), new DateOnly(2027, 1, 1)], months);
    }

    [Fact]
    public void Route_CheckSchedulesNextByInterval()
    {
        var route = Route("IST", "MAD");

        route.RecordCheckSuccess(Now);

        Assert.False(route.IsDueForCheck(Now.AddHours(5)));
        Assert.True(route.IsDueForCheck(Now.AddHours(6)));
    }

    [Fact]
    public void Deal_SameFlightAndPrice_ProducesSameKey()
    {
        var first = Deal(2450m);
        var second = Deal(2450.40m);

        Assert.Equal(first.DealKey, second.DealKey);
        Assert.NotEqual(first.DealKey, Deal(2100m).DealKey);
    }

    [Fact]
    public void Deal_PublishTwice_IsIdempotent_AndCannotBeDismissedAfter()
    {
        var deal = Deal(2450m);
        var messageId = Guid.NewGuid();

        deal.MarkAsPublished(messageId);
        deal.MarkAsPublished(Guid.NewGuid());

        Assert.Equal(messageId, deal.ChannelMessageId);
        Assert.Throws<DomainException>(deal.Dismiss);
    }

    private static FlightRoute Route(string origin, string destination, int monthsAhead = 3) =>
        FlightRoute.Create(
            AirportCode.Create(origin), AirportCode.Create(destination), null, 3000m, monthsAhead, TimeSpan.FromHours(6), false, Now);

    private static readonly Guid RouteId = Guid.CreateVersion7();

    private static FlightDeal Deal(decimal price) =>
        FlightDeal.Found(
            RouteId,
            AirportCode.Create("IST"),
            AirportCode.Create("MAD"),
            new DateTimeOffset(2026, 10, 12, 6, 55, 0, TimeSpan.FromHours(3)),
            price,
            "try",
            "PC",
            "1234",
            0,
            WebUrl.Create("https://www.aviasales.com/search/IST1210MAD1"),
            Now);
}
