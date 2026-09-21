using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Infrastructure.Persistence;

internal sealed class UnitOfWork(
    AppDbContext dbContext,
    IPublisher publisher,
    ILogger<UnitOfWork> logger) : IUnitOfWork
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var domainEvents = CollectDomainEvents();

        int affectedRows;

        try
        {
            affectedRows = await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException("Kayıt siz okuduktan sonra başka bir işlem tarafından değiştirildi.", exception);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new UniqueConstraintViolationException("Tekil olması gereken bir değer zaten kayıtlı.", exception);
        }

        // Event'ler ancak kayit basariliysa yayinlanir. Veritabaninda olmayan bir olay icin yan etki calismasin.
        await PublishDomainEventsAsync(domainEvents, cancellationToken);

        return affectedRows;
    }

    private List<IDomainEvent> CollectDomainEvents()
    {
        var aggregates = dbContext.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();

        var domainEvents = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToList();

        aggregates.ForEach(aggregate => aggregate.ClearDomainEvents());

        return domainEvents;
    }

    private async Task PublishDomainEventsAsync(List<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
            var notification = (INotification)Activator.CreateInstance(notificationType, domainEvent)!;

            try
            {
                await publisher.Publish(notification, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Veri zaten kaydedildi. Bir yan etkinin hatasi ana islemi basarisiz gostermemeli.
                logger.LogError(exception, "Domain event işlenemedi: {EventType} {EventId}", domainEvent.GetType().Name, domainEvent.EventId);
            }
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation };
    }
}
