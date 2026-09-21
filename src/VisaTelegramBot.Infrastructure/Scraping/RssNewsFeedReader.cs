using System.Xml;
using System.Xml.Linq;
using VisaTelegramBot.Application.Abstractions.Scraping;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Infrastructure.Scraping;

/// <summary>
/// RSS 2.0, Atom ve RSS 1.0 (RDF) beslemelerini okur.
/// System.ServiceModel.Syndication yerine hosgorulu bir XDocument ayristiricisi kullanilir:
/// gercek dunyadaki beslemeler sik sik standart disi tarih ve yapi icerir, katı ayristirici tum beslemeyi reddeder.
/// </summary>
internal sealed class RssNewsFeedReader(FeedDownloader downloader) : INewsFeedReader
{
    private static readonly XmlReaderSettings XmlSettings = new()
    {
        // XXE (XML External Entity) saldirilarina karsi DTD islenmez.
        DtdProcessing = DtdProcessing.Ignore,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        Async = true
    };

    private static readonly string[] ExcerptMarkers = ["[…]", "[...]"];

    public SourceType SourceType => SourceType.Rss;

    public async Task<IReadOnlyList<FeedEntry>> ReadAsync(NewsSource source, CancellationToken cancellationToken)
    {
        var content = await downloader.DownloadAsync(source.Url.ToUri(), cancellationToken);

        XDocument document;

        try
        {
            using var stream = new MemoryStream(content.Body, writable: false);
            using var reader = XmlReader.Create(stream, XmlSettings);
            document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
        }
        catch (XmlException exception)
        {
            throw new FeedReadException($"Besleme geçerli bir XML değil: {exception.Message}", exception);
        }

        var root = document.Root ?? throw new FeedReadException("Besleme boş.");
        var baseUri = content.FinalUri;

        return root.Name.LocalName switch
        {
            "rss" => ParseItems(Child(root, "channel")?.Elements().Where(IsNamed("item")) ?? [], baseUri, ParseRssItem),
            "feed" => ParseItems(root.Elements().Where(IsNamed("entry")), baseUri, ParseAtomEntry),
            "RDF" => ParseItems(root.Elements().Where(IsNamed("item")), baseUri, ParseRssItem),
            _ => throw new FeedReadException($"Tanınmayan besleme biçimi: <{root.Name.LocalName}>.")
        };
    }

    private static List<FeedEntry> ParseItems(
        IEnumerable<XElement> elements,
        Uri baseUri,
        Func<XElement, Uri, FeedEntry> parse)
    {
        return elements.Select(element => parse(element, baseUri)).ToList();
    }

    private static FeedEntry ParseRssItem(XElement item, Uri baseUri)
    {
        var link = Text(item, "link");

        if (string.IsNullOrWhiteSpace(link))
        {
            // Bazi beslemeler linki sadece guid icinde verir.
            var guid = Child(item, "guid");
            var isPermaLink = guid?.Attribute("isPermaLink")?.Value;

            if (guid is not null && !string.Equals(isPermaLink, "false", StringComparison.OrdinalIgnoreCase))
            {
                link = guid.Value;
            }
        }

        return new FeedEntry(
            HtmlText.ToPlainText(Text(item, "title")),
            HtmlText.ResolveLink(baseUri, link)?.AbsoluteUri,
            TrimExcerptMarker(HtmlText.ToPlainText(Text(item, "description") ?? Text(item, "encoded"))),
            FeedDateParser.TryParse(Text(item, "pubDate") ?? Text(item, "date")));
    }

    private static FeedEntry ParseAtomEntry(XElement entry, Uri baseUri)
    {
        var link = entry.Elements()
            .Where(IsNamed("link"))
            .Select(element => new
            {
                Rel = element.Attribute("rel")?.Value,
                Href = element.Attribute("href")?.Value
            })
            .Where(candidate => candidate.Rel is null or "alternate")
            .Select(candidate => candidate.Href)
            .FirstOrDefault();

        return new FeedEntry(
            HtmlText.ToPlainText(Text(entry, "title")),
            HtmlText.ResolveLink(baseUri, link)?.AbsoluteUri,
            HtmlText.ToPlainText(Text(entry, "summary") ?? Text(entry, "content")),
            FeedDateParser.TryParse(Text(entry, "published") ?? Text(entry, "updated")));
    }

    /// <summary>
    /// WordPress kisaltilmis ozetin sonuna "[…]" veya "[...]" ekler. Kanalda kirik gorundugu icin tek "…" ile degistirilir.
    /// </summary>
    private static string? TrimExcerptMarker(string? summary)
    {
        if (summary is null)
        {
            return null;
        }

        var trimmed = summary.TrimEnd();

        foreach (var marker in ExcerptMarkers)
        {
            if (trimmed.EndsWith(marker, StringComparison.Ordinal))
            {
                return trimmed[..^marker.Length].TrimEnd() + "…";
            }
        }

        return summary;
    }

    // Namespace'ler beslemeden beslemeye tutarsiz oldugu icin yerel ada gore esleriz.
    private static Func<XElement, bool> IsNamed(string localName) =>
        element => element.Name.LocalName == localName;

    private static XElement? Child(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(IsNamed(localName));

    private static string? Text(XElement parent, string localName) =>
        Child(parent, localName)?.Value;
}
