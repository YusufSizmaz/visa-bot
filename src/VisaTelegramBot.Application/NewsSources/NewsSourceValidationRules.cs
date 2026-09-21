using FluentValidation;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Application.NewsSources;

/// <summary>
/// Olusturma ve guncelleme ayni alan kurallarini paylasir. Kurallar tek yerde (DRY) ve
/// sinirlar domain sabitlerinden okunur, boylece iki katman birbirinden kopmaz.
/// </summary>
internal static class NewsSourceValidationRules
{
    public static IRuleBuilderOptions<T, string> ValidName<T>(this IRuleBuilder<T, string> rule)
    {
        return rule
            .NotEmpty().WithMessage("Kaynak adı boş olamaz.")
            .MaximumLength(NewsSource.NameMaxLength)
            .WithMessage($"Kaynak adı en fazla {NewsSource.NameMaxLength} karakter olabilir.");
    }

    public static IRuleBuilderOptions<T, string> ValidUrl<T>(this IRuleBuilder<T, string> rule)
    {
        return rule
            .Must(url => WebUrl.TryCreate(url, out _, out _))
            .WithMessage("Adres geçerli bir http veya https adresi olmalı.");
    }

    public static IRuleBuilderOptions<T, int> ValidFetchInterval<T>(this IRuleBuilder<T, int> rule)
    {
        var min = (int)NewsSource.MinFetchInterval.TotalMinutes;
        var max = (int)NewsSource.MaxFetchInterval.TotalMinutes;

        return rule
            .InclusiveBetween(min, max)
            .WithMessage($"Çekim aralığı {min} ile {max} dakika arasında olmalı.");
    }

    public static IRuleBuilderOptions<T, IReadOnlyList<string>?> ValidKeywords<T>(this IRuleBuilder<T, IReadOnlyList<string>?> rule)
    {
        return rule
            .Must(keywords => keywords is null || keywords.Count <= KeywordFilter.MaxKeywords)
            .WithMessage($"En fazla {KeywordFilter.MaxKeywords} anahtar kelime tanımlanabilir.")
            .Must(keywords => keywords is null || keywords.All(keyword => (keyword?.Trim().Length ?? 0) <= KeywordFilter.KeywordMaxLength))
            .WithMessage($"Anahtar kelimeler en fazla {KeywordFilter.KeywordMaxLength} karakter olabilir.");
    }

    public static void ValidEndpoint<T>(
        this AbstractValidator<T> validator,
        Func<T, SourceType> type,
        Func<T, HtmlParsingRulesDto?> rules)
    {
        validator.RuleFor(x => type(x))
            .IsInEnum()
            .WithName("Type")
            .WithMessage("Geçersiz kaynak tipi.");

        validator.RuleFor(x => rules(x))
            .NotNull()
            .When(x => type(x) == SourceType.Html)
            .WithName("ParsingRules")
            .WithMessage("HTML kaynakları için ayrıştırma kuralları zorunludur.");

        validator.RuleFor(x => rules(x))
            .Null()
            .When(x => type(x) == SourceType.Rss)
            .WithName("ParsingRules")
            .WithMessage("RSS kaynakları ayrıştırma kuralı almaz.");

        validator.RuleFor(x => rules(x)!.ItemSelector)
            .NotEmpty()
            .MaximumLength(HtmlParsingRules.SelectorMaxLength)
            .When(x => rules(x) is not null)
            .WithName("ParsingRules.ItemSelector");

        validator.RuleFor(x => rules(x)!.TitleSelector)
            .NotEmpty()
            .MaximumLength(HtmlParsingRules.SelectorMaxLength)
            .When(x => rules(x) is not null)
            .WithName("ParsingRules.TitleSelector");
    }
}
