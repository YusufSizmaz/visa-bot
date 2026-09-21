using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;
using VisaTelegramBot.Application.FlightDeals;
using VisaTelegramBot.Domain.FlightDeals;

namespace VisaTelegramBot.Infrastructure.FlightPrices;

public sealed class TravelpayoutsOptions
{
    public const string SectionName = "Travelpayouts";

    /// <summary>Travelpayouts panelindeki API token. Bos ise ucus kontrolleri anlasilir bir hatayla basarisiz olur.</summary>
    public string ApiToken { get; init; } = string.Empty;

    /// <summary>Ortaklik (affiliate) numarasi. Tanimliysa bilet linklerine eklenir ve satislardan komisyon alinir.</summary>
    public string? Marker { get; init; }

    public string Currency { get; init; } = "try";

    public string Market { get; init; } = "tr";

    /// <summary>API'nin dondurdugu goreli link bu adresin sonuna eklenir.</summary>
    public string LinkBaseUrl { get; init; } = "https://www.aviasales.com";
}

/// <summary>
/// Travelpayouts (Aviasales) Data API istemcisi. Son 48 saatte kullanicilarin buldugu en ucuz fiyatlari doner;
/// canli rezervasyon fiyati degildir, bu yuzden mesajlarda "fiyatlar degisebilir" uyarisi yer alir.
/// </summary>
internal sealed class TravelpayoutsFlightPriceProvider(HttpClient httpClient, IOptions<TravelpayoutsOptions> options)
    : IFlightPriceProvider
{
    public const string HttpClientName = "travelpayouts";
    private const int PageLimit = 30;

    public async Task<IReadOnlyList<FlightOffer>> GetCheapestAsync(
        AirportCode origin,
        AirportCode destination,
        DateOnly departureMonth,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.ApiToken))
        {
            throw new FlightPriceException("Travelpayouts:ApiToken tanımlı değil. Travelpayouts panelinden token alıp user-secrets ile girin.");
        }

        var query = string.Join('&',
            $"origin={origin.Value}",
            $"destination={destination.Value}",
            $"departure_at={departureMonth:yyyy-MM}",
            "one_way=true",
            "sorting=price",
            $"limit={PageLimit}",
            $"currency={Uri.EscapeDataString(settings.Currency)}",
            $"market={Uri.EscapeDataString(settings.Market)}");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/aviasales/v3/prices_for_dates?{query}");
        request.Headers.Add("X-Access-Token", settings.ApiToken);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new FlightPriceException("Travelpayouts API token geçersiz (401).");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new FlightPriceException($"Travelpayouts HTTP {(int)response.StatusCode} döndü.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            return Parse(document.RootElement, settings);
        }
        catch (JsonException exception)
        {
            throw new FlightPriceException("Travelpayouts yanıtı okunamadı.", exception);
        }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutRejectedException or BrokenCircuitException
                                          || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new FlightPriceException($"Travelpayouts'a ulaşılamadı: {exception.Message}", exception);
        }
    }

    internal static IReadOnlyList<FlightOffer> Parse(JsonElement root, TravelpayoutsOptions settings)
    {
        if (root.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.False)
        {
            var error = root.TryGetProperty("error", out var errorElement) ? errorElement.ToString() : "bilinmeyen hata";
            throw new FlightPriceException($"Travelpayouts hata döndü: {error}");
        }

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var currency = root.TryGetProperty("currency", out var currencyElement) ? currencyElement.GetString() ?? settings.Currency : settings.Currency;
        var offers = new List<FlightOffer>();

        foreach (var item in data.EnumerateArray())
        {
            if (!TryGetDecimal(item, "price", out var price)
                || !TryGetString(item, "departure_at", out var departureText)
                || !DateTimeOffset.TryParse(departureText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var departureAt)
                || !TryGetString(item, "link", out var link))
            {
                continue;
            }

            offers.Add(new FlightOffer(
                TryGetString(item, "origin", out var origin) ? origin : string.Empty,
                TryGetString(item, "destination", out var destination) ? destination : string.Empty,
                departureAt,
                price,
                currency.ToUpperInvariant(),
                TryGetString(item, "airline", out var airline) ? airline : null,
                // flight_number bazen sayi bazen metin olarak gelir.
                TryGetString(item, "flight_number", out var flightNumber) ? flightNumber : null,
                TryGetInt(item, "transfers", out var transfers) ? transfers : 0,
                BuildBookingUrl(link, settings)));
        }

        return offers;
    }

    internal static string BuildBookingUrl(string link, TravelpayoutsOptions settings)
    {
        var url = settings.LinkBaseUrl.TrimEnd('/') + (link.StartsWith('/') ? link : "/" + link);

        if (string.IsNullOrWhiteSpace(settings.Marker))
        {
            return url;
        }

        return url + (url.Contains('?') ? "&" : "?") + "marker=" + Uri.EscapeDataString(settings.Marker.Trim());
    }

    private static bool TryGetString(JsonElement item, string name, out string value)
    {
        value = string.Empty;

        if (!item.TryGetProperty(name, out var element))
        {
            return false;
        }

        value = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            _ => string.Empty
        };

        return value.Length > 0;
    }

    private static bool TryGetDecimal(JsonElement item, string name, out decimal value)
    {
        value = 0;
        return item.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out value);
    }

    private static bool TryGetInt(JsonElement item, string name, out int value)
    {
        value = 0;
        return item.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
    }
}
