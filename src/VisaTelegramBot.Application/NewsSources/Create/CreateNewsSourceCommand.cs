using FluentValidation;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Application.NewsSources.Create;

public sealed record CreateNewsSourceCommand(
    string Name,
    string Url,
    SourceType Type,
    HtmlParsingRulesDto? ParsingRules,
    int FetchIntervalMinutes,
    IReadOnlyList<string>? Keywords = null,
    bool TurkishOnly = false,
    SourceCategory Category = SourceCategory.Visa) : ICommand<Guid>;

internal sealed class CreateNewsSourceCommandValidator : AbstractValidator<CreateNewsSourceCommand>
{
    public CreateNewsSourceCommandValidator()
    {
        RuleFor(x => x.Name).ValidName();
        RuleFor(x => x.Url).ValidUrl();
        RuleFor(x => x.FetchIntervalMinutes).ValidFetchInterval();
        RuleFor(x => x.Keywords).ValidKeywords();
        RuleFor(x => x.Category).IsInEnum();
        this.ValidEndpoint(x => x.Type, x => x.ParsingRules);
    }
}
