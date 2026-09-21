using VisaTelegramBot.Application.Abstractions.Caching;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.Common.Results;

namespace VisaTelegramBot.Application.NewsSources.ChangeStatus;

public sealed record ActivateNewsSourceCommand(Guid NewsSourceId) : ICommand;

internal sealed class ActivateNewsSourceCommandHandler(
    INewsSourceRepository newsSourceRepository,
    IUnitOfWork unitOfWork,
    ICacheService cache,
    TimeProvider timeProvider) : ICommandHandler<ActivateNewsSourceCommand>
{
    public async Task<Result> Handle(ActivateNewsSourceCommand command, CancellationToken cancellationToken)
    {
        var newsSource = await newsSourceRepository.GetByIdAsync(command.NewsSourceId, cancellationToken);

        if (newsSource is null)
        {
            return NewsSourceErrors.NotFound(command.NewsSourceId);
        }

        newsSource.Activate(timeProvider.GetUtcNow().UtcDateTime);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.RemoveByTagAsync(NewsSourceCache.Tag, cancellationToken);

        return Result.Success();
    }
}
