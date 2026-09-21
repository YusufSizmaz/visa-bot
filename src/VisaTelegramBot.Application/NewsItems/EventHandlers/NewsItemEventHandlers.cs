using Microsoft.Extensions.Logging;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Domain.NewsItems.Events;

namespace VisaTelegramBot.Application.NewsItems.EventHandlers;

internal sealed class NewsItemDiscoveredEventHandler(ILogger<NewsItemDiscoveredEventHandler> logger)
    : IDomainEventHandler<NewsItemDiscoveredDomainEvent>
{
    public Task Handle(
        DomainEventNotification<NewsItemDiscoveredDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Yeni haber kuyruğa alındı: {Title} ({NewsItemId})",
            notification.DomainEvent.Title,
            notification.DomainEvent.NewsItemId);

        return Task.CompletedTask;
    }
}

internal sealed class NewsItemDeliveryFailedEventHandler(ILogger<NewsItemDeliveryFailedEventHandler> logger)
    : IDomainEventHandler<NewsItemDeliveryFailedDomainEvent>
{
    public Task Handle(
        DomainEventNotification<NewsItemDeliveryFailedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        logger.LogError(
            "Haber {NewsItemId} {Attempts} denemeden sonra gönderilemedi. Son hata: {LastError}",
            domainEvent.NewsItemId,
            domainEvent.DeliveryAttempts,
            domainEvent.LastError);

        return Task.CompletedTask;
    }
}
