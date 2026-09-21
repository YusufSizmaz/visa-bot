using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Domain.NewsSources.Events;

/// <summary>
/// Kaynak ust uste basarisiz oldugu icin otomatik olarak pasife alindi.
/// </summary>
public sealed record NewsSourceDeactivatedDomainEvent(
    Guid NewsSourceId,
    string NewsSourceName,
    int ConsecutiveFailureCount,
    string? LastError,
    DateTime OccurredOnUtc) : DomainEvent(OccurredOnUtc);
