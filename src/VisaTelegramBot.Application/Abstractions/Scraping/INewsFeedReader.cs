using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Application.Abstractions.Scraping;

/// <summary>
/// Strategy deseni: her kaynak tipi (RSS, HTML) icin ayri bir okuyucu.
/// Yeni bir tip eklemek icin yeni bir sinif yazilir, mevcut kod degismez (Open/Closed prensibi).
/// </summary>
public interface INewsFeedReader
{
    SourceType SourceType { get; }

    /// <exception cref="FeedReadException">Kaynak indirilemez veya ayristirilamazsa.</exception>
    Task<IReadOnlyList<FeedEntry>> ReadAsync(NewsSource source, CancellationToken cancellationToken);
}

public interface INewsFeedReaderResolver
{
    INewsFeedReader Resolve(SourceType sourceType);
}

/// <summary>Kaynak okunamadiginda firlatilir. Okuyucular altyapi hatalarini bu tipe sarar.</summary>
public sealed class FeedReadException(string message, Exception? innerException = null)
    : Exception(message, innerException);
