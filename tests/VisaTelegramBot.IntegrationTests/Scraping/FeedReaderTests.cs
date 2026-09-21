using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using NSubstitute;
using VisaTelegramBot.Application.Abstractions.Scraping;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsSources;
using VisaTelegramBot.Infrastructure.Scraping;

namespace VisaTelegramBot.IntegrationTests.Scraping;

/// <summary>
/// Okuyuculari gercek HTTP istegi atmadan, sahte bir HttpMessageHandler ile test ederiz.
/// Boylece testler hizli, deterministik ve internetten bagimsizdir.
/// </summary>
public sealed class FeedReaderTests
{
    private static readonly DateTime Now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Rss_ParsesItems_ResolvesRelativeLinks_StripsHtml()
    {
        const string rss = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0" xmlns:content="http://purl.org/rss/1.0/modules/content/">
              <channel>
                <title>Vize Haberleri</title>
                <item>
                  <title>Almanya &amp; Fransa randevu</title>
                  <link>/news/almanya-fransa</link>
                  <description><![CDATA[<p>Yeni <b>kurallar</b> açıklandı.</p>]]></description>
                  <pubDate>Wed, 09 Sep 2026 08:30:00 GMT</pubDate>
                </item>
                <item>
                  <title>Sadece guid</title>
                  <guid isPermaLink="true">https://example.com/news/guid-only</guid>
                  <pubDate>Wed, 09 Sep 2026 10:30:00 EST</pubDate>
                </item>
              </channel>
            </rss>
            """;

        var reader = new RssNewsFeedReader(Downloader(rss, "application/rss+xml"));

        var entries = await reader.ReadAsync(Source(SourceType.Rss), CancellationToken.None);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Almanya & Fransa randevu", entries[0].Title);
        Assert.Equal("https://example.com/news/almanya-fransa", entries[0].Link);
        Assert.Equal("Yeni kurallar açıklandı.", entries[0].Summary);
        Assert.Equal(new DateTime(2026, 9, 9, 8, 30, 0, DateTimeKind.Utc), entries[0].PublishedAtUtc);
        Assert.Equal("https://example.com/news/guid-only", entries[1].Link);
        Assert.Equal(new DateTime(2026, 9, 9, 15, 30, 0, DateTimeKind.Utc), entries[1].PublishedAtUtc);
    }

    [Fact]
    public async Task Rss_WordPressExcerpt_ReplacesTrailingMarkerWithEllipsis()
    {
        // ucuzaucak.net "ucak-bileti" beslemesinden kisaltilmis gercek bir kayit.
        const string rss = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
              <channel>
                <item>
                  <title>İstanbul Maldivler Uçak Bileti</title>
                  <link>https://ucuzaucak.net/ucak-bileti/istanbul-maldivler-ucak-bileti-32/</link>
                  <guid isPermaLink="false">https://ucuzaucak.net/?post_type=ucak-bileti&amp;p=29250</guid>
                  <description><![CDATA[THY ile aktarmasız gidiş dönüş ucuza uçuşlar 28.827₺ 🔥 ✅ 8 kg kabin + 23 kg alt bagaj [&#8230;]]]></description>
                  <pubDate>Mon, 14 Sep 2026 10:13:14 +0000</pubDate>
                </item>
              </channel>
            </rss>
            """;

        var reader = new RssNewsFeedReader(Downloader(rss, "application/rss+xml"));

        var entry = Assert.Single(await reader.ReadAsync(Source(SourceType.Rss), CancellationToken.None));

        Assert.Equal("https://ucuzaucak.net/ucak-bileti/istanbul-maldivler-ucak-bileti-32/", entry.Link);
        Assert.Equal("THY ile aktarmasız gidiş dönüş ucuza uçuşlar 28.827₺ 🔥 ✅ 8 kg kabin + 23 kg alt bagaj…", entry.Summary);
    }

    [Fact]
    public async Task Atom_ParsesAlternateLinkAndPublishedDate()
    {
        const string atom = """
            <?xml version="1.0" encoding="utf-8"?>
            <feed xmlns="http://www.w3.org/2005/Atom">
              <entry>
                <title>Schengen ETIAS takvimi</title>
                <link rel="self" href="https://example.com/self"/>
                <link rel="alternate" href="https://example.com/etias"/>
                <summary>Özet metni</summary>
                <published>2026-09-08T10:00:00+03:00</published>
              </entry>
            </feed>
            """;

        var reader = new RssNewsFeedReader(Downloader(atom, "application/atom+xml"));

        var entry = Assert.Single(await reader.ReadAsync(Source(SourceType.Rss), CancellationToken.None));

        Assert.Equal("https://example.com/etias", entry.Link);
        Assert.Equal(new DateTime(2026, 9, 8, 7, 0, 0, DateTimeKind.Utc), entry.PublishedAtUtc);
    }

    [Fact]
    public async Task Rss_InvalidXml_ThrowsFeedReadException()
    {
        var reader = new RssNewsFeedReader(Downloader("<html>bu bir besleme değil", "text/html"));

        await Assert.ThrowsAsync<FeedReadException>(() => reader.ReadAsync(Source(SourceType.Rss), CancellationToken.None));
    }

    [Fact]
    public async Task HttpError_ThrowsFeedReadException()
    {
        var reader = new RssNewsFeedReader(Downloader(string.Empty, "text/plain", HttpStatusCode.ServiceUnavailable));

        var exception = await Assert.ThrowsAsync<FeedReadException>(() => reader.ReadAsync(Source(SourceType.Rss), CancellationToken.None));

        Assert.Contains("503", exception.Message);
    }

    [Fact]
    public async Task Html_ParsesItemsWithSelectors()
    {
        const string html = """
            <html><body>
              <article class="news">
                <h2><a href="/duyuru/1">İtalya vize başvuruları</a></h2>
                <p class="summary">Başvurular iVisa üzerinden alınacak.</p>
                <time datetime="2026-09-07T09:00:00Z">7 Eylül</time>
              </article>
              <article class="news">
                <h2>Linksiz başlık</h2>
              </article>
            </body></html>
            """;

        var rules = HtmlParsingRules.Create("article.news", "h2", summarySelector: "p.summary", publishedAtSelector: "time");
        var reader = new HtmlNewsFeedReader(Downloader(html, "text/html; charset=utf-8"));

        var entries = await reader.ReadAsync(Source(SourceType.Html, rules), CancellationToken.None);

        Assert.Equal(2, entries.Count);
        Assert.Equal("İtalya vize başvuruları", entries[0].Title);
        Assert.Equal("https://example.com/duyuru/1", entries[0].Link);
        Assert.Equal("Başvurular iVisa üzerinden alınacak.", entries[0].Summary);
        Assert.Equal(new DateTime(2026, 9, 7, 9, 0, 0, DateTimeKind.Utc), entries[0].PublishedAtUtc);
        Assert.Null(entries[1].Link);
    }

    [Fact]
    public async Task Html_SonfiyatCampaignCards_ParsesAnchorItemsAndTurkishDates()
    {
        // sonfiyat.com/haberler/kampanyalar kartlarinin sadelestirilmis hali. Kaynak HTML'deki "<p><p>" hatasi bilerek korunur.
        const string html = """
            <html><body><div class="box">
              <a href="/haber/thy-eylul-2026-kampanya-ucak-bileti" class="flx">
                <div class="fol_sm">
                  <h4 class="fnt-bold">THY Eyl&#252;l 2026 Kampanyası: Avrupa ve Kafkasya&#39;da U&#231;ak Bileti Fırsatı</h4>
                  <div class="fow mrg-a-0">
                    <p><p>THY Avrupa ve Kafkasya uçuşlarında indirimli bilet kampanyası başlattı.</p></p>
                    <p>08.09.2026</p>
                  </div>
                </div>
              </a>
            </div></body></html>
            """;

        var rules = HtmlParsingRules.Create(
            "div.box > a[href^=\"/haber/\"]",
            "h4",
            summarySelector: "div.fow > p:not(:empty):not(:last-child)",
            publishedAtSelector: "div.fow > p:last-child");
        var reader = new HtmlNewsFeedReader(Downloader(html, "text/html; charset=utf-8"));

        var entry = Assert.Single(await reader.ReadAsync(Source(SourceType.Html, rules), CancellationToken.None));

        Assert.Equal("THY Eylül 2026 Kampanyası: Avrupa ve Kafkasya'da Uçak Bileti Fırsatı", entry.Title);
        Assert.Equal("https://example.com/haber/thy-eylul-2026-kampanya-ucak-bileti", entry.Link);
        Assert.Equal("THY Avrupa ve Kafkasya uçuşlarında indirimli bilet kampanyası başlattı.", entry.Summary);
        Assert.Equal(new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc), entry.PublishedAtUtc);
    }

    [Fact]
    public async Task Html_InvalidSelector_ThrowsFeedReadException()
    {
        var rules = HtmlParsingRules.Create("article[[", "h2");
        var reader = new HtmlNewsFeedReader(Downloader("<html></html>", "text/html"));

        await Assert.ThrowsAsync<FeedReadException>(() => reader.ReadAsync(Source(SourceType.Html, rules), CancellationToken.None));
    }

    private static NewsSource Source(SourceType type, HtmlParsingRules? rules = null) =>
        NewsSource.Create("Test", WebUrl.Create("https://example.com/feed"), type, rules, TimeSpan.FromMinutes(15), Now);

    private static FeedDownloader Downloader(string body, string contentType, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHandler(() =>
        {
            var content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
            content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            return new HttpResponseMessage(status) { Content = content };
        });

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, disposeHandler: false));

        return new FeedDownloader(factory, Options.Create(new ScrapingOptions()));
    }

    private sealed class StubHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = responseFactory();
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}
