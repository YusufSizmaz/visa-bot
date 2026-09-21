using VisaTelegramBot.Application.Abstractions.Caching;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Application.NewsSources.Create;

internal sealed class CreateNewsSourceCommandHandler(
    INewsSourceRepository newsSourceRepository,
    IUnitOfWork unitOfWork,
    ICacheService cache,
    TimeProvider timeProvider) : ICommandHandler<CreateNewsSourceCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateNewsSourceCommand command, CancellationToken cancellationToken)
    {
        NewsSource newsSource;

        try
        {
            var url = WebUrl.Create(command.Url);

            if (await newsSourceRepository.ExistsByUrlAsync(url, excludingId: null, cancellationToken))
            {
                return NewsSourceErrors.UrlAlreadyExists;
            }

            newsSource = NewsSource.Create(
                command.Name,
                url,
                command.Type,
                command.ParsingRules?.ToDomain(),
                TimeSpan.FromMinutes(command.FetchIntervalMinutes),
                timeProvider.GetUtcNow().UtcDateTime,
                KeywordFilter.CreateOrNull(command.Keywords),
                command.TurkishOnly,
                command.Category);
        }
        catch (DomainException exception)
        {
            // Validator cogu durumu yakalar; bu, domain kurallarinin son savunma hatti (defense in depth).
            return NewsSourceErrors.InvalidState(exception.Message);
        }

        newsSourceRepository.Add(newsSource);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.RemoveByTagAsync(NewsSourceCache.Tag, cancellationToken);

        return newsSource.Id;
    }
}
