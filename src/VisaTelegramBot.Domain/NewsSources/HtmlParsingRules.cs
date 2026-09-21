using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Domain.NewsSources;

/// <summary>
/// RSS vermeyen siteler icin CSS secicileri. Sadece Html tipindeki kaynaklarda bulunur.
/// </summary>
public sealed class HtmlParsingRules : ValueObject
{
    public const int SelectorMaxLength = 300;

    private HtmlParsingRules(
        string itemSelector,
        string titleSelector,
        string? linkSelector,
        string? summarySelector,
        string? publishedAtSelector)
    {
        ItemSelector = itemSelector;
        TitleSelector = titleSelector;
        LinkSelector = linkSelector;
        SummarySelector = summarySelector;
        PublishedAtSelector = publishedAtSelector;
    }

    /// <summary>Her bir haber kartini secen ifade. Ornek: "article.news-item"</summary>
    public string ItemSelector { get; }

    /// <summary>Kartin icindeki basligi secen ifade. Ornek: "h2"</summary>
    public string TitleSelector { get; }

    /// <summary>Kartin icindeki linki secen ifade. Bos ise basliktaki veya karttaki ilk a etiketi kullanilir.</summary>
    public string? LinkSelector { get; }

    public string? SummarySelector { get; }

    /// <summary>Tarih elementini secen ifade. datetime niteligi varsa o, yoksa metin okunur.</summary>
    public string? PublishedAtSelector { get; }

    public static HtmlParsingRules Create(
        string? itemSelector,
        string? titleSelector,
        string? linkSelector = null,
        string? summarySelector = null,
        string? publishedAtSelector = null)
    {
        return new HtmlParsingRules(
            Required(itemSelector, nameof(ItemSelector)),
            Required(titleSelector, nameof(TitleSelector)),
            Optional(linkSelector, nameof(LinkSelector)),
            Optional(summarySelector, nameof(SummarySelector)),
            Optional(publishedAtSelector, nameof(PublishedAtSelector)));
    }

    private static string Required(string? selector, string name)
    {
        return Optional(selector, name) ?? throw new DomainException($"{name} boş olamaz.");
    }

    private static string? Optional(string? selector, string name)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return null;
        }

        var trimmed = selector.Trim();

        if (trimmed.Length > SelectorMaxLength)
        {
            throw new DomainException($"{name} en fazla {SelectorMaxLength} karakter olabilir.");
        }

        return trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ItemSelector;
        yield return TitleSelector;
        yield return LinkSelector;
        yield return SummarySelector;
        yield return PublishedAtSelector;
    }
}
