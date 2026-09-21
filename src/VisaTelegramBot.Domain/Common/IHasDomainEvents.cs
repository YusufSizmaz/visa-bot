namespace VisaTelegramBot.Domain.Common;

/// <summary>
/// Generic olmayan erisim noktasi. Altyapi, tipini bilmeden tum aggregate'lerin event'lerini toplayabilsin diye.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
