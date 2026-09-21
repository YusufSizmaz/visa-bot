using System.Globalization;
using System.Text;
using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Domain.NewsSources;

/// <summary>
/// Kaynagin sadece ilgili haberlerini kanala gondermek icin anahtar kelime filtresi.
/// Eslesme buyuk/kucuk harf ve aksan duyarsizdir ("RANDEVU", "randevusu", "Randevuları" hepsi "randevu" ile eslesir).
/// Kelimeler kelime basindan eslenir: "cita" anahtari "citas" ile eslesir ama "felicitaciones" ile eslesmez.
/// </summary>
public sealed class KeywordFilter : ValueObject
{
    public const int MaxKeywords = 50;
    public const int KeywordMaxLength = 60;

    private readonly string[][] _keywordTokens;

    private KeywordFilter(IReadOnlyList<string> keywords)
    {
        Keywords = keywords;
        _keywordTokens = keywords.Select(Tokenize).ToArray();
    }

    public IReadOnlyList<string> Keywords { get; }

    /// <summary>Liste bos veya null ise filtre yok demektir ve null doner.</summary>
    public static KeywordFilter? CreateOrNull(IEnumerable<string?>? keywords)
    {
        if (keywords is null)
        {
            return null;
        }

        var cleaned = keywords
            .Select(keyword => keyword?.Trim())
            .Where(keyword => !string.IsNullOrEmpty(keyword))
            .Select(keyword => keyword!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleaned.Count == 0)
        {
            return null;
        }

        if (cleaned.Count > MaxKeywords)
        {
            throw new DomainException($"En fazla {MaxKeywords} anahtar kelime tanımlanabilir.");
        }

        foreach (var keyword in cleaned)
        {
            if (keyword.Length > KeywordMaxLength)
            {
                throw new DomainException($"Anahtar kelime en fazla {KeywordMaxLength} karakter olabilir: {keyword}");
            }

            if (Tokenize(keyword).Length == 0)
            {
                throw new DomainException($"Anahtar kelime harf veya rakam içermeli: {keyword}");
            }
        }

        return new KeywordFilter(cleaned);
    }

    public bool Matches(params string?[] texts)
    {
        var tokens = texts
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .SelectMany(text => Tokenize(text!))
            .ToArray();

        return _keywordTokens.Any(keyword => ContainsPhrase(tokens, keyword));
    }

    private static bool ContainsPhrase(string[] tokens, string[] phrase)
    {
        for (var start = 0; start <= tokens.Length - phrase.Length; start++)
        {
            var matched = true;

            for (var offset = 0; offset < phrase.Length; offset++)
            {
                if (!tokens[start + offset].StartsWith(phrase[offset], StringComparison.Ordinal))
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Metni karsilastirilabilir kelimelere boler: kucuk harf, Turkce i/ı birlestirme ve aksan temizligi.
    /// </summary>
    private static string[] Tokenize(string text)
    {
        var normalized = text
            .Replace('İ', 'i')
            .Replace('I', 'i')
            .Replace('ı', 'i')
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var keyword in Keywords)
        {
            yield return keyword.ToUpperInvariant();
        }
    }
}
