using System.Text.RegularExpressions;

namespace VisaTelegramBot.Domain.Common;

/// <summary>
/// Metnin Turkce olup olmadigini hafif bir kuralla tahmin eder.
/// ğ, ı ve ş harfleri Ispanyolca, Italyanca ve Ingilizcede kullanilmaz; bunlardan biri varsa metin Turkcedir.
/// Yoksa Turkceye ozgu sik kelimelere bakilir ("vize", "randevu", "için"...). ASCII'ye cevrilmis Turkce basliklari da yakalar.
/// Tam bir dil tanima kutuphanesi degildir; kisa basliklarda amaca yetecek kadar isabetlidir.
/// </summary>
public static partial class TurkishTextDetector
{
    private static readonly string[] TurkishWordPrefixes =
    [
        "ve", "bir", "bu", "icin", "için", "ile", "olarak", "veya", "gibi", "hakkinda", "hakkında",
        "iliskin", "ilişkin", "tarafindan", "tarafından", "yeni", "vize", "randevu", "basvuru", "başvuru",
        "duyuru", "onemli", "önemli", "vatandas", "vatandaş", "konsolosluk", "buyukelcilik", "büyükelçilik",
        "ogrenci", "öğrenci", "sayili", "sayılı", "tarihli", "kapsaminda", "kapsamında", "gecis", "geçiş",
        "ulke", "ülke", "turkiye", "türkiye", "universite", "üniversite", "akademik", "yili", "yılı"
    ];

    private static readonly HashSet<string> ShortExactWords = new(StringComparer.Ordinal) { "ve", "bir", "bu", "ile" };

    public static bool IsTurkish(params string?[] texts)
    {
        var text = string.Join(' ', texts.Where(value => !string.IsNullOrWhiteSpace(value)));

        if (text.Length == 0)
        {
            return false;
        }

        if (TurkishOnlyLetters().IsMatch(text))
        {
            return true;
        }

        var words = WordSplitter().Split(text.ToLowerInvariant())
            .Where(word => word.Length > 0)
            .ToArray();

        var hits = words.Count(IsTurkishWord);

        return hits >= 2 || (words.Length <= 3 && hits >= 1);
    }

    private static bool IsTurkishWord(string word)
    {
        if (ShortExactWords.Contains(word))
        {
            return true;
        }

        return TurkishWordPrefixes.Any(prefix => prefix.Length > 3 && word.StartsWith(prefix, StringComparison.Ordinal));
    }

    [GeneratedRegex("[ğĞışŞİ]")]
    private static partial Regex TurkishOnlyLetters();

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex WordSplitter();
}
