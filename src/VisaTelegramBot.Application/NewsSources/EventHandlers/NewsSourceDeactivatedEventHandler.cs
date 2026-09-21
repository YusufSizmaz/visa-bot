using Microsoft.Extensions.Logging;
using VisaTelegramBot.Application.Abstractions.Caching;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Domain.NewsSources.Events;

namespace VisaTelegramBot.Application.NewsSources.EventHandlers;

/// <summary>
/// Domain event'e tepki veren yan etki. Aggregate sadece "pasife alindim" der; ne yapilacagina burada karar verilir.
/// </summary>
internal sealed class NewsSourceDeactivatedEventHandler(
    ICacheService cache,
    ILogger<NewsSourceDeactivatedEventHandler> logger) : IDomainEventHandler<NewsSourceDeactivatedDomainEvent>
{
    public async Task Handle(
        DomainEventNotification<NewsSourceDeactivatedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        logger.LogWarning(
            "Kaynak {NewsSourceName} ({NewsSourceId}) art arda {FailureCount} hata sonrası pasife alındı. Son hata: {LastError}",
            domainEvent.NewsSourceName,
            domainEvent.NewsSourceId,
            domainEvent.ConsecutiveFailureCount,
            domainEvent.LastError);

        await cache.RemoveByTagAsync(NewsSourceCache.Tag, cancellationToken);
    }
}
