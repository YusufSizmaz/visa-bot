using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Application.Abstractions.Persistence;

/// <summary>
/// Repository: aggregate'i butun olarak yukleyip kaydeden koleksiyon benzeri arayuz.
/// Sadece yazma tarafinda (command) kullanilir; okuma tarafi INewsSourceQueries uzerinden gider.
/// </summary>
public interface INewsSourceRepository
{
    Task<NewsSource?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByUrlAsync(WebUrl url, Guid? excludingId, CancellationToken cancellationToken);

    void Add(NewsSource newsSource);
}
