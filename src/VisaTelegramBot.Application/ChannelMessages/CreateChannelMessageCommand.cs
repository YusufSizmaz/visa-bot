using FluentValidation;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Domain.ChannelMessages;
using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Application.ChannelMessages;

public sealed record CreateChannelMessageCommand(
    string? Title,
    string Body,
    string? LinkUrl,
    string? ButtonText,
    string? ButtonUrl,
    byte[]? PhotoContent,
    string? PhotoFileName,
    DateTime? ScheduledAtUtc) : ICommand<Guid>;

internal sealed class CreateChannelMessageCommandValidator : AbstractValidator<CreateChannelMessageCommand>
{
    public CreateChannelMessageCommandValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Mesaj metni boş olamaz.")
            .MaximumLength(ChannelMessage.BodyMaxLength);

        RuleFor(x => x.Title).MaximumLength(ChannelMessage.TitleMaxLength);

        RuleFor(x => x.LinkUrl)
            .Must(url => WebUrl.TryCreate(url, out _, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.LinkUrl))
            .WithMessage("Link geçerli bir http veya https adresi olmalı.");

        RuleFor(x => x.ButtonText)
            .NotEmpty().When(x => !string.IsNullOrWhiteSpace(x.ButtonUrl))
            .WithMessage("Buton adresi girildiyse buton metni de girilmeli.")
            .MaximumLength(MessageButton.TextMaxLength);

        RuleFor(x => x.ButtonUrl)
            .Must(url => WebUrl.TryCreate(url, out _, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.ButtonText))
            .WithMessage("Buton için geçerli bir http veya https adresi girilmeli.");

        RuleFor(x => x.PhotoContent)
            .Must(content => content!.Length <= MessagePhoto.MaxSizeBytes)
            .When(x => x.PhotoContent is not null)
            .WithMessage($"Görsel en fazla {MessagePhoto.MaxSizeBytes / (1024 * 1024)} MB olabilir.");
    }
}

internal sealed class CreateChannelMessageCommandHandler(
    IChannelMessageRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateChannelMessageCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateChannelMessageCommand command, CancellationToken cancellationToken)
    {
        ChannelMessage message;

        try
        {
            var button = string.IsNullOrWhiteSpace(command.ButtonText)
                ? null
                : MessageButton.Create(command.ButtonText, WebUrl.Create(command.ButtonUrl));

            message = ChannelMessage.Create(
                ChannelMessageKind.Custom,
                command.Title,
                command.Body,
                string.IsNullOrWhiteSpace(command.LinkUrl) ? null : WebUrl.Create(command.LinkUrl),
                button,
                command.PhotoContent is { Length: > 0 } photo ? MessagePhoto.Create(photo, command.PhotoFileName) : null,
                command.ScheduledAtUtc,
                timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (DomainException exception)
        {
            return ChannelMessageErrors.InvalidState(exception.Message);
        }

        repository.Add(message);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return message.Id;
    }
}
