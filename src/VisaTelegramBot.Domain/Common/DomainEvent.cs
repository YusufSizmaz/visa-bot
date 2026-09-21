namespace VisaTelegramBot.Domain.Common;

/// <summary>
/// Tum domain event'lerinin ortak tabani. Event'ler degismez veri tasiyicilaridir, bu yuzden record.
/// </summary>
public abstract record DomainEvent(DateTime OccurredOnUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();
}
