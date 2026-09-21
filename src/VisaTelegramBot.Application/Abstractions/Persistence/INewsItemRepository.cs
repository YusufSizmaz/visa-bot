using VisaTelegramBot.Domain.NewsItems;

namespace VisaTelegramBot.Application.Abstractions.Persistence;

public interface INewsItemRepository
{
    Task<NewsItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Verilen parmak izlerinden veritabaninda zaten bulunanlari tek sorguda doner.</summary>
    Task<IReadOnlySet<string>> GetExistingContentHashesAsync(
        IReadOnlyCollection<ContentHash> contentHashes,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gonderime hazir haberleri atomik olarak kiralar ve kimliklerini doner.
    /// Ayni anda calisan birden fazla worker ayni haberi asla birlikte almaz (competing consumers).
    /// </summary>
    Task<IReadOnlyList<Guid>> ClaimForDeliveryAsync(
        int batchSize,
        DateTime utcNow,
        DateTime lockedUntilUtc,
        CancellationToken cancellationToken);

    void AddRange(IEnumerable<NewsItem> newsItems);
}
