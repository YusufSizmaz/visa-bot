using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Domain.NewsItems.Events;

/// <summary>
/// Haber kanala gonderilemedi ve artik yeniden denenmeyecek.
/// </summary>
public sealed record NewsItemDeliveryFailedDomainEvent(
    Guid NewsItemId,
    int DeliveryAttempts,
    string? LastError,
    DateTime OccurredOnUtc) : DomainEvent(OccurredOnUtc);
