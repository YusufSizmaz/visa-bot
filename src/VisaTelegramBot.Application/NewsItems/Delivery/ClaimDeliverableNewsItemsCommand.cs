using FluentValidation;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.Common.Results;

namespace VisaTelegramBot.Application.NewsItems.Delivery;

/// <summary>
/// Gonderime hazir haberlerden bir grubu bu worker adina kiralar.
/// Kira suresi dolana kadar baska worker bu haberleri almaz; worker coker ise kira dolar ve haber tekrar alinir.
/// </summary>
public sealed record ClaimDeliverableNewsItemsCommand(int BatchSize, TimeSpan LeaseDuration)
    : ICommand<IReadOnlyList<Guid>>;

internal sealed class ClaimDeliverableNewsItemsCommandValidator : AbstractValidator<ClaimDeliverableNewsItemsCommand>
{
    public ClaimDeliverableNewsItemsCommandValidator()
    {
        RuleFor(x => x.BatchSize).InclusiveBetween(1, 500);
        RuleFor(x => x.LeaseDuration).InclusiveBetween(TimeSpan.FromSeconds(10), TimeSpan.FromHours(1));
    }
}

internal sealed class ClaimDeliverableNewsItemsCommandHandler(
    INewsItemRepository newsItemRepository,
    TimeProvider timeProvider) : ICommandHandler<ClaimDeliverableNewsItemsCommand, IReadOnlyList<Guid>>
{
    public async Task<Result<IReadOnlyList<Guid>>> Handle(
        ClaimDeliverableNewsItemsCommand command,
        CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var ids = await newsItemRepository.ClaimForDeliveryAsync(
            command.BatchSize,
            utcNow,
            utcNow + command.LeaseDuration,
            cancellationToken);

        return Result.Success(ids);
    }
}
