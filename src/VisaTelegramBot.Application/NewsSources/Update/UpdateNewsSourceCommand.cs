using FluentValidation;
using VisaTelegramBot.Application.Abstractions.Caching;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Application.NewsSources.Update;

public sealed record UpdateNewsSourceCommand(
    Guid NewsSourceId,
    string Name,
    string Url,
    SourceType Type,
    HtmlParsingRulesDto? ParsingRules,
    int FetchIntervalMinutes,
    IReadOnlyList<string>? Keywords = null,
    bool TurkishOnly = false,
    SourceCategory Category = SourceCategory.Visa) : ICommand;

internal sealed class UpdateNewsSourceCommandValidator : AbstractValidator<UpdateNewsSourceCommand>
{
    public UpdateNewsSourceCommandValidator()
    {
        RuleFor(x => x.NewsSourceId).NotEmpty();
        RuleFor(x => x.Name).ValidName();
        RuleFor(x => x.Url).ValidUrl();
        RuleFor(x => x.FetchIntervalMinutes).ValidFetchInterval();
        RuleFor(x => x.Keywords).ValidKeywords();
        RuleFor(x => x.Category).IsInEnum();
        this.ValidEndpoint(x => x.Type, x => x.ParsingRules);
    }
}

internal sealed class UpdateNewsSourceCommandHandler(
    INewsSourceRepository newsSourceRepository,
    IUnitOfWork unitOfWork,
    ICacheService cache) : ICommandHandler<UpdateNewsSourceCommand>
{
    public async Task<Result> Handle(UpdateNewsSourceCommand command, CancellationToken cancellationToken)
    {
        var newsSource = await newsSourceRepository.GetByIdAsync(command.NewsSourceId, cancellationToken);

        if (newsSource is null)
        {
            return NewsSourceErrors.NotFound(command.NewsSourceId);
        }

        try
        {
            var url = WebUrl.Create(command.Url);

            if (await newsSourceRepository.ExistsByUrlAsync(url, newsSource.Id, cancellationToken))
            {
                return NewsSourceErrors.UrlAlreadyExists;
            }

            // Tek bir "Update" setter'i yerine niyet bildiren davranislar cagrilir.
            newsSource.Rename(command.Name);
            newsSource.ChangeEndpoint(url, command.Type, command.ParsingRules?.ToDomain());
            newsSource.ChangeFetchInterval(TimeSpan.FromMinutes(command.FetchIntervalMinutes));
            newsSource.ChangeKeywordFilter(KeywordFilter.CreateOrNull(command.Keywords));
            newsSource.ChangeLanguagePolicy(command.TurkishOnly);
            newsSource.ChangeCategory(command.Category);
        }
        catch (DomainException exception)
        {
            return NewsSourceErrors.InvalidState(exception.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.RemoveByTagAsync(NewsSourceCache.Tag, cancellationToken);

        return Result.Success();
    }
}
