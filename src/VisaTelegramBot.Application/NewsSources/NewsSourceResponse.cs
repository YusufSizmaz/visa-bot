using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Application.NewsSources;

/// <summary>Okuma modeli (DTO). Domain nesnesi disari hic sizmaz.</summary>
public sealed record NewsSourceResponse(
    Guid Id,
    string Name,
    string Url,
    SourceType Type,
    HtmlParsingRulesDto? ParsingRules,
    IReadOnlyList<string>? Keywords,
    bool TurkishOnly,
    SourceCategory Category,
    int FetchIntervalMinutes,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime NextFetchAtUtc,
    DateTime? LastFetchedAtUtc,
    DateTime? LastSucceededAtUtc,
    int ConsecutiveFailureCount,
    string? LastFetchError);

public sealed record HtmlParsingRulesDto(
    string ItemSelector,
    string TitleSelector,
    string? LinkSelector,
    string? SummarySelector,
    string? PublishedAtSelector)
{
    public static HtmlParsingRulesDto? FromDomain(HtmlParsingRules? rules)
    {
        return rules is null
            ? null
            : new HtmlParsingRulesDto(
                rules.ItemSelector,
                rules.TitleSelector,
                rules.LinkSelector,
                rules.SummarySelector,
                rules.PublishedAtSelector);
    }

    public HtmlParsingRules ToDomain()
    {
        return HtmlParsingRules.Create(ItemSelector, TitleSelector, LinkSelector, SummarySelector, PublishedAtSelector);
    }
}
