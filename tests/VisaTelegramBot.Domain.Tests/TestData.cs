using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsItems;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Domain.Tests;

internal static class TestData
{
    public static readonly DateTime Now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    public static NewsSource RssSource(TimeSpan? interval = null) => NewsSource.Create(
        "Schengen Haberleri",
        WebUrl.Create("https://example.com/feed"),
        SourceType.Rss,
        parsingRules: null,
        interval ?? TimeSpan.FromMinutes(15),
        Now);

    public static NewsItem PendingItem() => NewsItem.Discover(
        Guid.CreateVersion7(),
        "Almanya vize randevu sistemini değiştirdi",
        WebUrl.Create("https://example.com/news/1"),
        "Özet",
        Now.AddHours(-1),
        Now);
}
