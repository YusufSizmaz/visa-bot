using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Domain.ChannelMessages;
using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Application.ChannelMessages;

public enum ChannelMessageAction
{
    Cancel = 1,
    SendNow = 2,
    Retry = 3
}

/// <summary>Iptal, hemen gonder ve yeniden dene: ayni yukle, ayni kaydet akisi; tek komutta toplandi.</summary>
public sealed record ChangeChannelMessageCommand(Guid ChannelMessageId, ChannelMessageAction Action) : ICommand;

internal sealed class ChangeChannelMessageCommandHandler(
    IChannelMessageRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ChangeChannelMessageCommand>
{
    public async Task<Result> Handle(ChangeChannelMessageCommand command, CancellationToken cancellationToken)
    {
        var message = await repository.GetByIdAsync(command.ChannelMessageId, cancellationToken);

        if (message is null)
        {
            return ChannelMessageErrors.NotFound(command.ChannelMessageId);
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            Apply(message, command.Action, utcNow);
        }
        catch (DomainException exception)
        {
            return ChannelMessageErrors.InvalidState(exception.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static void Apply(ChannelMessage message, ChannelMessageAction action, DateTime utcNow)
    {
        switch (action)
        {
            case ChannelMessageAction.Cancel:
                message.Cancel();
                break;
            case ChannelMessageAction.SendNow:
                message.SendNow(utcNow);
                break;
            case ChannelMessageAction.Retry:
                message.Retry(utcNow);
                break;
            default:
                throw new DomainException("Geçersiz işlem.");
        }
    }
}
