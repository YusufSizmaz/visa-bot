using FluentValidation;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.Abstractions.Publishing;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Application.NewsItems.Delivery;
using VisaTelegramBot.Domain.ChannelMessages;

namespace VisaTelegramBot.Application.ChannelMessages;

public sealed record ClaimDueChannelMessagesCommand(int BatchSize, TimeSpan LeaseDuration) : ICommand<IReadOnlyList<Guid>>;

internal sealed class ClaimDueChannelMessagesCommandValidator : AbstractValidator<ClaimDueChannelMessagesCommand>
{
    public ClaimDueChannelMessagesCommandValidator()
    {
        RuleFor(x => x.BatchSize).InclusiveBetween(1, 100);
        RuleFor(x => x.LeaseDuration).InclusiveBetween(TimeSpan.FromSeconds(10), TimeSpan.FromHours(1));
    }
}

internal sealed class ClaimDueChannelMessagesCommandHandler(IChannelMessageRepository repository, TimeProvider timeProvider)
    : ICommandHandler<ClaimDueChannelMessagesCommand, IReadOnlyList<Guid>>
{
    public async Task<Result<IReadOnlyList<Guid>>> Handle(ClaimDueChannelMessagesCommand command, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var ids = await repository.ClaimDueAsync(command.BatchSize, utcNow, utcNow + command.LeaseDuration, cancellationToken);

        return Result.Success(ids);
    }
}

public sealed record DeliverChannelMessageCommand(Guid ChannelMessageId) : ICommand<DeliverNewsItemResult>;

/// <summary>Haber teslimatiyla ayni sonuc modeli (DeliveryOutcome) kullanilir; Worker ikisini ayni sekilde ele alir.</summary>
internal sealed class DeliverChannelMessageCommandHandler(
    IChannelMessageRepository repository,
    INewsPublisher publisher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<DeliverChannelMessageCommand, DeliverNewsItemResult>
{
    private static readonly TimeSpan DefaultRateLimitDelay = TimeSpan.FromSeconds(5);

    public async Task<Result<DeliverNewsItemResult>> Handle(DeliverChannelMessageCommand command, CancellationToken cancellationToken)
    {
        var message = await repository.GetByIdAsync(command.ChannelMessageId, cancellationToken);

        if (message is null)
        {
            return ChannelMessageErrors.NotFound(command.ChannelMessageId);
        }

        if (message.Status != ChannelMessageStatus.Scheduled)
        {
            return new DeliverNewsItemResult(message.Id, DeliveryOutcome.AlreadyProcessed);
        }

        var post = new ChannelPost(
            message.Id,
            message.Title,
            message.Body,
            message.LinkUrl?.Value,
            message.Button is null ? null : new PostButton(message.Button.Text, message.Button.Url.Value),
            message.Photo is null ? null : new PostPhoto(message.Photo.Content, message.Photo.ContentType, message.Photo.FileName));

        var result = await publisher.PublishPostAsync(post, cancellationToken);
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        DeliveryOutcome outcome;

        switch (result.Outcome)
        {
            case PublishOutcome.Published:
                message.MarkAsSent(result.ExternalMessageId, utcNow);
                outcome = DeliveryOutcome.Delivered;
                break;
            case PublishOutcome.RateLimited:
                message.Defer(result.RetryAfter ?? DefaultRateLimitDelay, utcNow);
                outcome = DeliveryOutcome.Deferred;
                break;
            case PublishOutcome.PermanentFailure:
                message.RecordFailure(result.Error ?? "Kalıcı hata.", isPermanent: true, utcNow);
                outcome = DeliveryOutcome.Failed;
                break;
            default:
                message.RecordFailure(result.Error ?? "Geçici hata.", isPermanent: false, utcNow);
                outcome = message.Status == ChannelMessageStatus.Failed ? DeliveryOutcome.Failed : DeliveryOutcome.RetryScheduled;
                break;
        }

        // Mesaj gonderildiyse bu bilgi iptal sinyali yuzunden kaybolmasin; aksi halde tekrar gonderilir.
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        return new DeliverNewsItemResult(message.Id, outcome, result.RetryAfter);
    }
}
