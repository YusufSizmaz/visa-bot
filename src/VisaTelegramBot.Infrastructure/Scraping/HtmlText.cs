using AngleSharp.Html.Parser;

namespace VisaTelegramBot.Infrastructure.Scraping;

internal static class HtmlText
{
    /// <summary>
    /// Beslemelerdeki ozetler cogu zaman HTML icerir. Etiketleri atar ve duz metin doner.
    /// Telegram'a asla kaynaktan gelen ham HTML gondermeyiz.
    /// </summary>
    public static string? ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        if (!html.Contains('<') && !html.Contains('&'))
        {
            return html;
        }

        var parser = new HtmlParser();
        using var document = parser.ParseDocument($"<!doctype html><html><body>{html}</body></html>");

        foreach (var element in document.QuerySelectorAll("script, style, noscript"))
        {
            element.Remove();
        }

        return document.Body?.TextContent;
    }

    public static Uri? ResolveLink(Uri baseUri, string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        return Uri.TryCreate(baseUri, href.Trim(), out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
                ? absolute
                : null;
    }
}
