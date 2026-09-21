using System.Globalization;
using VisaTelegramBot.Domain.ChannelMessages;
using VisaTelegramBot.Domain.FlightDeals;

namespace VisaTelegramBot.Application.FlightDeals;

/// <summary>
/// Firsati Turkce bir kanal mesajina donusturur. Factory deseni: mesajin nasil kurulacagi tek yerde,
/// hem elle yayinlamada hem otomatik yayinda ayni metin uretilir.
/// </summary>
public static class FlightDealMessageFactory
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static ChannelMessage Create(FlightDeal deal, FlightRoute route, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(deal);
        ArgumentNullException.ThrowIfNull(route);

        var title = $"✈️ {route.Label} · {FormatPrice(deal.Price, deal.Currency)}";

        var transfers = deal.Transfers == 0 ? "Direkt uçuş" : $"{deal.Transfers} aktarmalı";
        var airline = deal.Airline is null ? null : $"🛫 Havayolu: {deal.Airline}{(deal.FlightNumber is null ? string.Empty : $" {deal.FlightNumber}")}";

        var lines = new List<string>
        {
            $"📅 Gidiş: {deal.DepartureAt.ToString("d MMMM yyyy dddd, HH:mm", Turkish)}",
            $"🔁 {transfers}",
            $"📍 {deal.Origin} → {deal.Destination}",
        };

        if (airline is not null)
        {
            lines.Add(airline);
        }

        lines.Add(string.Empty);
        lines.Add("Fiyatlar anlık değişebilir; güncel fiyatı bilet sayfasında kontrol edin.");

        return ChannelMessage.Create(
            ChannelMessageKind.FlightDeal,
            title,
            string.Join('\n', lines),
            linkUrl: null,
            MessageButton.Create("Bileti incele", deal.BookingUrl),
            photo: null,
            scheduledAtUtc: null,
            utcNow);
    }

    public static string FormatPrice(decimal price, string currency)
    {
        var amount = price.ToString("#,0", Turkish);

        return currency.ToUpperInvariant() switch
        {
            "TRY" => $"{amount} ₺",
            "EUR" => $"{amount} €",
            "USD" => $"{amount} $",
            _ => $"{amount} {currency}"
        };
    }
}
