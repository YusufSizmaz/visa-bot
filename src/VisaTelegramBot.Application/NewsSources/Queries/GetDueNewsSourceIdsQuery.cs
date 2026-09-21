using FluentValidation;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Common.Results;

namespace VisaTelegramBot.Application.NewsSources.Queries;

/// <summary>Cekim zamani gelmis aktif kaynaklarin kimlikleri. Zamanlayici bunu periyodik olarak sorar.</summary>
public sealed record GetDueNewsSourceIdsQuery(int Limit) : IQuery<IReadOnlyList<Guid>>;

internal sealed class GetDueNewsSourceIdsQueryValidator : AbstractValidator<GetDueNewsSourceIdsQuery>
{
    public GetDueNewsSourceIdsQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 1000);
    }
}

internal sealed class GetDueNewsSourceIdsQueryHandler(INewsSourceQueries queries, TimeProvider timeProvider)
    : IQueryHandler<GetDueNewsSourceIdsQuery, IReadOnlyList<Guid>>
{
    public async Task<Result<IReadOnlyList<Guid>>> Handle(
        GetDueNewsSourceIdsQuery query,
        CancellationToken cancellationToken)
    {
        var ids = await queries.GetDueIdsAsync(timeProvider.GetUtcNow().UtcDateTime, query.Limit, cancellationToken);

        return Result.Success(ids);
    }
}
