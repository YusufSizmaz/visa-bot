using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Domain.NewsItems.Events;

/// <summary>
/// Yeni bir haber bulundu ve kanala gonderilmek uzere kuyruga alindi.
/// </summary>
public sealed record NewsItemDiscoveredDomainEvent(
    Guid NewsItemId,
    Guid NewsSourceId,
    string Title,
    DateTime OccurredOnUtc) : DomainEvent(OccurredOnUtc);
