using System.Globalization;

namespace VisaTelegramBot.Infrastructure.Scraping;

/// <summary>
/// Beslemelerdeki tarih bicimleri standarda pek uymaz. RFC 822 ("Tue, 10 Sep 2026 12:00:00 GMT"),
/// ISO 8601 ("2026-09-10T12:00:00Z"), saat dilimi kisaltmalari ve Turkce ay adlari desteklenir.
/// Cozulemeyen tarih hata sayilmaz, null doner.
/// </summary>
internal static class FeedDateParser
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly string[] DayFirstDottedFormats = ["d.M.yyyy", "d.M.yyyy HH:mm", "d.M.yyyy HH:mm:ss"];

    private static readonly (string Abbreviation, string Offset)[] TimeZoneAbbreviations =
    [
        ("UTC", "+00:00"), ("GMT", "+00:00"), ("UT", "+00:00"), ("Z", "+00:00"),
        ("EST", "-05:00"), ("EDT", "-04:00"), ("CST", "-06:00"), ("CDT", "-05:00"),
        ("MST", "-07:00"), ("MDT", "-06:00"), ("PST", "-08:00"), ("PDT", "-07:00"),
        ("BST", "+01:00"), ("CET", "+01:00"), ("CEST", "+02:00"), ("EET", "+02:00"),
        ("EEST", "+03:00"), ("MSK", "+03:00"), ("TRT", "+03:00"), ("IST", "+05:30")
    ];

    public static DateTime? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();

        // "08.09.2026" InvariantCulture ile 9 Agustos okunur. Noktali sayisal tarih pratikte hep gun.ay.yil oldugu icin once o denenir.
        if (DateTimeOffset.TryParseExact(text, DayFirstDottedFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var dotted))
        {
            return dotted.UtcDateTime;
        }

        if (TryParseWithCulture(text, CultureInfo.InvariantCulture, out var result)
            || TryParseWithCulture(ReplaceTimeZoneAbbreviation(text), CultureInfo.InvariantCulture, out result)
            || TryParseWithCulture(text, TurkishCulture, out result))
        {
            return result;
        }

        return null;
    }

    private static bool TryParseWithCulture(string text, CultureInfo culture, out DateTime utc)
    {
        const DateTimeStyles styles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal;

        if (DateTimeOffset.TryParse(text, culture, styles, out var parsed))
        {
            utc = parsed.UtcDateTime;
            return true;
        }

        utc = default;
        return false;
    }

    private static string ReplaceTimeZoneAbbreviation(string text)
    {
        foreach (var (abbreviation, offset) in TimeZoneAbbreviations)
        {
            var suffix = " " + abbreviation;

            if (text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat(text.AsSpan(0, text.Length - suffix.Length), " ", offset);
            }
        }

        return text;
    }
}
