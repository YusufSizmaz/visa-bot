using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Application.Abstractions.Scraping;

/// <summary>
/// Kayitli tum okuyuculari DI'dan alir ve tipe gore dogru stratejiyi secer.
/// switch-case yerine sozluk: yeni okuyucu eklemek bu sinifi degistirmeyi gerektirmez.
/// </summary>
internal sealed class NewsFeedReaderResolver : INewsFeedReaderResolver
{
    private readonly Dictionary<SourceType, INewsFeedReader> _readers;

    public NewsFeedReaderResolver(IEnumerable<INewsFeedReader> readers)
    {
        _readers = readers.ToDictionary(reader => reader.SourceType);
    }

    public INewsFeedReader Resolve(SourceType sourceType)
    {
        return _readers.TryGetValue(sourceType, out var reader)
            ? reader
            : throw new InvalidOperationException($"'{sourceType}' tipi için kayıtlı bir okuyucu yok.");
    }
}
