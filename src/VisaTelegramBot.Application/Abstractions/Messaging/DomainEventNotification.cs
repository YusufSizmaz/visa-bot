using MediatR;
using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Application.Abstractions.Messaging;

/// <summary>
/// Domain katmani MediatR'i bilmez. Domain event'lerini MediatR bildirimine bu sarmalayici donusturur.
/// </summary>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;

public interface IDomainEventHandler<TDomainEvent> : INotificationHandler<DomainEventNotification<TDomainEvent>>
    where TDomainEvent : IDomainEvent;
