namespace VisaTelegramBot.Application.Abstractions.Persistence;

/// <summary>
/// Unit of Work: bir is akisindaki tum degisiklikleri tek seferde ve atomik olarak kaydeder.
/// Kayit basarili olursa aggregate'lerin domain event'lerini yayinlar.
/// </summary>
public interface IUnitOfWork
{
    /// <exception cref="ConcurrencyConflictException">Kayit baskasi tarafindan degistirildiyse.</exception>
    /// <exception cref="UniqueConstraintViolationException">Tekil (unique) kural ihlal edildiyse.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
