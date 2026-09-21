using FluentValidation;
using VisaTelegramBot.Application.Abstractions.Messaging;

namespace VisaTelegramBot.Application.NewsSources.Fetch;

/// <param name="NewsSourceId">Cekilecek kaynak.</param>
/// <param name="Force">True ise zamani gelmemis olsa bile cekilir (API'den elle tetikleme).</param>
public sealed record FetchNewsSourceCommand(Guid NewsSourceId, bool Force = false) : ICommand<FetchNewsSourceResult>;

public enum FetchStatus
{
    Completed = 1,
    Skipped = 2
}

public sealed record FetchNewsSourceResult(
    Guid NewsSourceId,
    FetchStatus Status,
    string? SkipReason,
    int EntriesRead,
    int NewItems,
    int QueuedForDelivery,
    int Archived)
{
    public static FetchNewsSourceResult Skipped(Guid newsSourceId, string reason) =>
        new(newsSourceId, FetchStatus.Skipped, reason, 0, 0, 0, 0);
}

internal sealed class FetchNewsSourceCommandValidator : AbstractValidator<FetchNewsSourceCommand>
{
    public FetchNewsSourceCommandValidator()
    {
        RuleFor(x => x.NewsSourceId).NotEmpty();
    }
}
