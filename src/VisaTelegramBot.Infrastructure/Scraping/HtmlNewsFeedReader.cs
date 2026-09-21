using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using VisaTelegramBot.Application.Abstractions.Scraping;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Infrastructure.Scraping;

/// <summary>
/// RSS vermeyen sayfalari CSS secicileriyle okur. Her kaynak kendi secicilerini veritabaninda tasir,
/// bu yuzden yeni bir site eklemek icin kod degil sadece kayit gerekir.
/// </summary>
internal sealed class HtmlNewsFeedReader(FeedDownloader downloader) : INewsFeedReader
{
    public SourceType SourceType => SourceType.Html;

    public async Task<IReadOnlyList<FeedEntry>> ReadAsync(NewsSource source, CancellationToken cancellationToken)
    {
        var rules = source.ParsingRules
            ?? throw new FeedReadException("HTML kaynağı için ayrıştırma kuralları tanımlı değil.");

        var content = await downloader.DownloadAsync(source.Url.ToUri(), cancellationToken);

        try
        {
            using var document = await ParseAsync(content, cancellationToken);

            return document
                .QuerySelectorAll(rules.ItemSelector)
                .Select(item => ParseItem(item, rules, content.FinalUri))
                .ToList();
        }
        catch (DomException exception)
        {
            throw new FeedReadException($"Geçersiz CSS seçici: {exception.Message}", exception);
        }
    }

    private static async Task<IHtmlDocument> ParseAsync(DownloadedContent content, CancellationToken cancellationToken)
    {
        var parser = new HtmlParser();

        // Sunucu karakter setini bildirdiyse ona guveniriz; bildirmediyse AngleSharp <meta charset> ile kendisi bulur.
        if (TryGetEncoding(content.CharSet, out var encoding))
        {
            return await parser.ParseDocumentAsync(encoding.GetString(content.Body), cancellationToken);
        }

        using var stream = new MemoryStream(content.Body, writable: false);
        return await parser.ParseDocumentAsync(stream, cancellationToken);
    }

    private static FeedEntry ParseItem(IElement item, HtmlParsingRules rules, Uri baseUri)
    {
        var titleElement = item.QuerySelector(rules.TitleSelector);

        var linkElement = rules.LinkSelector is null
            ? FindLink(item, titleElement)
            : item.QuerySelector(rules.LinkSelector);

        var summary = rules.SummarySelector is null
            ? null
            : item.QuerySelector(rules.SummarySelector)?.TextContent;

        var dateElement = rules.PublishedAtSelector is null
            ? null
            : item.QuerySelector(rules.PublishedAtSelector);

        var dateText = dateElement?.GetAttribute("datetime") ?? dateElement?.TextContent;

        return new FeedEntry(
            titleElement?.TextContent,
            HtmlText.ResolveLink(baseUri, linkElement?.GetAttribute("href"))?.AbsoluteUri,
            summary,
            FeedDateParser.TryParse(dateText));
    }

    private static IElement? FindLink(IElement item, IElement? titleElement)
    {
        if (titleElement is IHtmlAnchorElement)
        {
            return titleElement;
        }

        return titleElement?.QuerySelector("a[href]")
            ?? titleElement?.Closest("a[href]")
            ?? (item is IHtmlAnchorElement ? item : null)
            ?? item.QuerySelector("a[href]");
    }

    private static bool TryGetEncoding(string? charSet, out Encoding encoding)
    {
        encoding = Encoding.UTF8;

        if (string.IsNullOrWhiteSpace(charSet))
        {
            return false;
        }

        try
        {
            encoding = Encoding.GetEncoding(charSet);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
