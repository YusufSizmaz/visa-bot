using VisaTelegramBot.Application.Abstractions.Caching;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Common.Results;

namespace VisaTelegramBot.Application.NewsSources.Queries;

public sealed record ListNewsSourcesQuery(bool? IsActive) : IQuery<IReadOnlyList<NewsSourceResponse>>;

internal sealed class ListNewsSourcesQueryHandler(INewsSourceQueries queries, ICacheService cache)
    : IQueryHandler<ListNewsSourcesQuery, IReadOnlyList<NewsSourceResponse>>
{
    public async Task<Result<IReadOnlyList<NewsSourceResponse>>> Handle(
        ListNewsSourcesQuery query,
        CancellationToken cancellationToken)
    {
        var items = await cache.GetOrCreateAsync(
            NewsSourceCache.List(query.IsActive),
            token => queries.ListAsync(query.IsActive, token),
            NewsSourceCache.Expiration,
            NewsSourceCache.Tags,
            cancellationToken);

        return Result.Success(items);
    }
}
