namespace VisaTelegramBot.Application.Abstractions.Locking;

/// <summary>
/// Dagitik kilit: uygulamanin birden fazla kopyasi calisirken ayni isi ayni anda tek bir kopyanin yapmasini saglar.
/// Tek makinedeki lock/SemaphoreSlim bunu yapamaz, cunku kopyalar ayri process'lerdir.
/// </summary>
public interface IDistributedLockProvider
{
    /// <summary>Kilidi beklemeden almaya calisir. Alinamazsa null doner. Donen nesne dispose edilince kilit birakilir.</summary>
    Task<IAsyncDisposable?> TryAcquireAsync(string resource, CancellationToken cancellationToken);
}
