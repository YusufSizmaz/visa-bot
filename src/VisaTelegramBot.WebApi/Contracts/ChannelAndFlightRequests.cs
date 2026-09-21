namespace VisaTelegramBot.WebApi.Contracts;

public sealed class CreateChannelMessageRequest
{
    public string? Title { get; init; }

    public string Body { get; init; } = string.Empty;

    public string? LinkUrl { get; init; }

    public string? ButtonText { get; init; }

    public string? ButtonUrl { get; init; }

    public IFormFile? Photo { get; init; }

    /// <summary>Bos ise hemen gonderilir. Saat dilimi bilgisiyle gelmeli (ISO 8601), orn. 2026-09-20T09:00:00+03:00.</summary>
    public DateTimeOffset? ScheduledAt { get; init; }
}

public sealed record SaveFlightRouteRequest(
    string Origin,
    string Destination,
    string? Label,
    decimal MaxPrice,
    int MonthsAhead = 3,
    int CheckIntervalMinutes = 360,
    bool AutoPublish = false);
