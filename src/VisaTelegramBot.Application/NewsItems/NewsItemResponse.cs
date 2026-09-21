using VisaTelegramBot.Domain.NewsItems;

namespace VisaTelegramBot.Application.NewsItems;

public sealed record NewsItemResponse(
    Guid Id,
    Guid NewsSourceId,
    string NewsSourceName,
    string Title,
    string Url,
    string? Summary,
    DateTime? PublishedAtUtc,
    DateTime DiscoveredAtUtc,
    DeliveryStatus DeliveryStatus,
    int DeliveryAttempts,
    DateTime NextDeliveryAttemptAtUtc,
    DateTime? DeliveredAtUtc,
    string? LastDeliveryError,
    ArchiveReason? ArchiveReason);
