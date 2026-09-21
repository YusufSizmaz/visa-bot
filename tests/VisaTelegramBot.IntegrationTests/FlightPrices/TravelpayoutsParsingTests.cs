using System.Text.Json;
using VisaTelegramBot.Application.Abstractions.Publishing;
using VisaTelegramBot.Application.FlightDeals;
using VisaTelegramBot.Infrastructure.FlightPrices;
using VisaTelegramBot.Infrastructure.Publishing.Telegram;

namespace VisaTelegramBot.IntegrationTests.FlightPrices;

public sealed class TravelpayoutsParsingTests
{
    private static readonly TravelpayoutsOptions Settings = new() { Marker = "12345", LinkBaseUrl = "https://www.aviasales.com" };

    [Fact]
    public void Parse_ReadsV3Tickets_AndBuildsAffiliateLinks()
    {
        const string json = """
            {
              "success": true,
              "currency": "try",
              "data": [
                {
                  "origin": "IST", "destination": "MAD", "origin_airport": "IST", "destination_airport": "MAD",
                  "price": 2450, "airline": "PC", "flight_number": 1234,
                  "departure_at": "2026-10-12T06:55:00+03:00", "transfers": 1, "duration": 330,
                  "link": "/search/IST1210MAD1?t=abc"
                },
                {
                  "origin": "IST", "destination": "MAD", "price": 3100, "airline": "TK", "flight_number": "1857",
                  "departure_at": "2026-10-15T09:10:00+03:00", "transfers": 0, "link": "/search/IST1510MAD1"
                },
                { "price": 999, "link": "/eksik-tarih" }
              ]
            }
            """;

        using var document = JsonDocument.Parse(json);
        var offers = TravelpayoutsFlightPriceProvider.Parse(document.RootElement, Settings);

        Assert.Equal(2, offers.Count);
        Assert.Equal(2450m, offers[0].Price);
        Assert.Equal("1234", offers[0].FlightNumber);
        Assert.Equal("TRY", offers[0].Currency);
        Assert.Equal(1, offers[0].Transfers);
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 6, 55, 0, TimeSpan.FromHours(3)), offers[0].DepartureAt);
        Assert.Equal("https://www.aviasales.com/search/IST1210MAD1?t=abc&marker=12345", offers[0].BookingUrl);
        Assert.Equal("https://www.aviasales.com/search/IST1510MAD1?marker=12345", offers[1].BookingUrl);
    }

    [Fact]
    public void Parse_ErrorResponse_Throws()
    {
        using var document = JsonDocument.Parse("""{ "success": false, "data": null, "error": "invalid token" }""");

        Assert.Throws<FlightPriceException>(() => TravelpayoutsFlightPriceProvider.Parse(document.RootElement, Settings));
    }

    [Fact]
    public void FormatPost_EscapesUserText_AndRespectsCaptionLimit()
    {
        var post = new ChannelPost(Guid.NewGuid(), "Başlık <b>", new string('ş', 2000) + " & son", "https://example.com/?a=1&b=2", null, null);

        var caption = TelegramMessageFormatter.FormatPost(post, TelegramMessageFormatter.MaxCaptionLength);
        var message = TelegramMessageFormatter.FormatPost(post, TelegramMessageFormatter.MaxMessageLength);

        Assert.True(caption.Length <= TelegramMessageFormatter.MaxCaptionLength);
        Assert.StartsWith("<b>Başlık &lt;b&gt;</b>", caption);
        Assert.EndsWith("\">Detaylar</a>", caption);
        Assert.Contains("a=1&amp;b=2", caption);
        Assert.Contains("&amp; son", message);
    }
}
