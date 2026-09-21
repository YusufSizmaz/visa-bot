using System.Globalization;
using System.Text;
using VisaTelegramBot.Application.Abstractions.Publishing;
using VisaTelegramBot.Application.Common;

namespace VisaTelegramBot.Infrastructure.Publishing.Telegram;

/// <summary>
/// Haberi Telegram HTML bicimine cevirir. Ayri ve saf (yan etkisiz) bir sinif oldugu icin kolayca test edilir.
/// </summary>
internal static class TelegramMessageFormatter
{
    public const int MaxMessageLength = 4096;
    public const int MaxCaptionLength = 1024;
    private const int MaxSummaryLength = 600;

    /// <summary>
    /// Serbest gonderi: kalin baslik, metin ve istege bagli link. Kullanicinin yazdigi metin HTML olarak yorumlanmaz,
    /// once kacirilir; boylece "&lt;" gibi karakterler mesaji bozmaz ve kimse HTML enjekte edemez.
    /// </summary>
    public static string FormatPost(ChannelPost post, int maxLength)
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(post.Title))
        {
            builder.Append("<b>").Append(Escape(post.Title)).Append("</b>\n\n");
        }

        var link = string.IsNullOrWhiteSpace(post.LinkUrl)
            ? string.Empty
            : $"\n\n🔗 <a href=\"{EscapeAttribute(post.LinkUrl)}\">Detaylar</a>";

        var fixedLength = builder.Length + link.Length;
        var body = Escape(post.Body);

        // Sinir asilirsa metin kisaltilir; kacis dizileri (&amp;) ortadan bolunmesin diye kacirilmamis metin kesilir.
        if (fixedLength + body.Length > maxLength)
        {
            var budget = Math.Max(0, maxLength - fixedLength - 1);

            // Kacis sadece uzatir; once butceye gore kes, sonra hala uzunsa sondan kirp.
            var raw = post.Body.Length > budget ? post.Body[..budget] : post.Body;

            while (raw.Length > 0 && Escape(raw).Length > budget)
            {
                raw = raw[..^1];
            }

            body = Escape(raw.TrimEnd()) + "…";
        }

        return builder.Append(body).Append(link).ToString();
    }

    public static string Format(NewsMessage message)
    {
        var text = Build(message, message.Summary is null ? null : TextTruncation.Truncate(message.Summary, MaxSummaryLength), message.Title);

        if (text.Length <= MaxMessageLength)
        {
            return text;
        }

        // Cok nadir: baslik asiri uzunsa once ozet atilir, sonra baslik kisaltilir.
        text = Build(message, summary: null, message.Title);

        return text.Length <= MaxMessageLength
            ? text
            : Build(message, summary: null, TextTruncation.Truncate(message.Title, 1000));
    }

    private static string Build(NewsMessage message, string? summary, string title)
    {
        var builder = new StringBuilder();

        var isCampaign = message.Category == Domain.NewsSources.SourceCategory.FlightCampaign;

        builder.Append(isCampaign ? "✈️ <b>" : "🛂 <b>").Append(Escape(title)).Append("</b>");

        if (!string.IsNullOrWhiteSpace(summary))
        {
            builder.Append("\n\n").Append(Escape(summary));
        }

        builder.Append("\n\n📰 ").Append(Escape(message.SourceName));

        if (message.PublishedAtUtc is { } publishedAt)
        {
            builder.Append(" · 🗓 ").Append(publishedAt.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
        }

        builder.Append("\n🔗 <a href=\"").Append(EscapeAttribute(message.Url)).Append("\">").Append(isCampaign ? "Kampanyayı gör" : "Habere git").Append("</a>");

        return builder.ToString();
    }

    /// <summary>Telegram HTML modunda sadece bu uc karakterin kacirilmasi gerekir.</summary>
    public static string Escape(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string EscapeAttribute(string value) =>
        Escape(value).Replace("\"", "&quot;", StringComparison.Ordinal);
}
