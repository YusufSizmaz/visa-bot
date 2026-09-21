using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Application.NewsItems.Requeue;

/// <summary>Basarisiz veya arsivlenmis bir haberi elle yeniden gonderim kuyruguna alir.</summary>
public sealed record RequeueNewsItemCommand(Guid NewsItemId) : ICommand;

internal sealed class RequeueNewsItemCommandHandler(
    INewsItemRepository newsItemRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RequeueNewsItemCommand>
{
    public async Task<Result> Handle(RequeueNewsItemCommand command, CancellationToken cancellationToken)
    {
        var item = await newsItemRepository.GetByIdAsync(command.NewsItemId, cancellationToken);

        if (item is null)
        {
            return NewsItemErrors.NotFound(command.NewsItemId);
        }

        try
        {
            item.RequeueForDelivery(timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (DomainException exception)
        {
            return NewsItemErrors.InvalidState(exception.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
