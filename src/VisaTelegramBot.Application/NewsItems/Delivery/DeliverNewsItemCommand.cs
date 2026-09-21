using FluentValidation;
using VisaTelegramBot.Application.Abstractions.Messaging;

namespace VisaTelegramBot.Application.NewsItems.Delivery;

public sealed record DeliverNewsItemCommand(Guid NewsItemId) : ICommand<DeliverNewsItemResult>;

public enum DeliveryOutcome
{
    Delivered = 1,

    /// <summary>Haber zaten gonderilmis, basarisiz veya arsivlenmis. Hicbir sey yapilmadi.</summary>
    AlreadyProcessed = 2,

    /// <summary>Hiz limiti nedeniyle ertelendi. Deneme hakki harcanmadi.</summary>
    Deferred = 3,

    /// <summary>Gecici hata; ileri bir zamanda yeniden denenecek.</summary>
    RetryScheduled = 4,

    /// <summary>Kalici hata veya deneme hakki bitti.</summary>
    Failed = 5
}

public sealed record DeliverNewsItemResult(Guid NewsItemId, DeliveryOutcome Outcome, TimeSpan? RetryAfter = null);

internal sealed class DeliverNewsItemCommandValidator : AbstractValidator<DeliverNewsItemCommand>
{
    public DeliverNewsItemCommandValidator()
    {
        RuleFor(x => x.NewsItemId).NotEmpty();
    }
}
