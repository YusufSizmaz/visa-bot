namespace VisaTelegramBot.Application.Abstractions.Caching;

/// <summary>
/// Cache-Aside deseni: once cache'e bak, yoksa kaynaktan uret ve cache'e yaz.
/// Implementasyon ayni anahtara gelen es zamanli istekleri tek uretime indirir (cache stampede korumasi).
/// </summary>
public interface ICacheService
{
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan expiration,
        IReadOnlyCollection<string>? tags,
        CancellationToken cancellationToken);

    Task RemoveByTagAsync(string tag, CancellationToken cancellationToken);
}
