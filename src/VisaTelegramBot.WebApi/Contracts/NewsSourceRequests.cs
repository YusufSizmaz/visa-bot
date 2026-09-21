using VisaTelegramBot.Application.NewsSources;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.WebApi.Contracts;

/// <summary>
/// API sozlesmesi (request) ile uygulama komutu (command) bilincli olarak ayri tiplerdir.
/// API'nin disari verdigi soz, ic yapidaki degisikliklerden etkilenmez.
/// </summary>
public sealed record CreateNewsSourceRequest(
    string Name,
    string Url,
    SourceType Type,
    HtmlParsingRulesDto? ParsingRules,
    int FetchIntervalMinutes = 15,
    IReadOnlyList<string>? Keywords = null,
    bool TurkishOnly = false,
    SourceCategory Category = SourceCategory.Visa);

public sealed record UpdateNewsSourceRequest(
    string Name,
    string Url,
    SourceType Type,
    HtmlParsingRulesDto? ParsingRules,
    int FetchIntervalMinutes,
    IReadOnlyList<string>? Keywords = null,
    bool TurkishOnly = false,
    SourceCategory Category = SourceCategory.Visa);

public sealed record CreatedResponse(Guid Id);
