using VisaTelegramBot.Application.Abstractions.Caching;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.Common.Results;

namespace VisaTelegramBot.Application.NewsSources.ChangeStatus;

public sealed record DeactivateNewsSourceCommand(Guid NewsSourceId) : ICommand;

internal sealed class DeactivateNewsSourceCommandHandler(
    INewsSourceRepository newsSourceRepository,
    IUnitOfWork unitOfWork,
    ICacheService cache) : ICommandHandler<DeactivateNewsSourceCommand>
{
    public async Task<Result> Handle(DeactivateNewsSourceCommand command, CancellationToken cancellationToken)
    {
        var newsSource = await newsSourceRepository.GetByIdAsync(command.NewsSourceId, cancellationToken);

        if (newsSource is null)
        {
            return NewsSourceErrors.NotFound(command.NewsSourceId);
        }

        newsSource.Deactivate();

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.RemoveByTagAsync(NewsSourceCache.Tag, cancellationToken);

        return Result.Success();
    }
}
