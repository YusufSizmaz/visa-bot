using VisaTelegramBot.Application.Abstractions.Caching;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Common.Results;

namespace VisaTelegramBot.Application.NewsSources.Queries;

public sealed record GetNewsSourceByIdQuery(Guid NewsSourceId) : IQuery<NewsSourceResponse>;

internal sealed class GetNewsSourceByIdQueryHandler(INewsSourceQueries queries, ICacheService cache)
    : IQueryHandler<GetNewsSourceByIdQuery, NewsSourceResponse>
{
    public async Task<Result<NewsSourceResponse>> Handle(GetNewsSourceByIdQuery query, CancellationToken cancellationToken)
    {
        var response = await cache.GetOrCreateAsync(
            NewsSourceCache.ById(query.NewsSourceId),
            token => queries.GetByIdAsync(query.NewsSourceId, token),
            NewsSourceCache.Expiration,
            NewsSourceCache.Tags,
            cancellationToken);

        return response is null
            ? NewsSourceErrors.NotFound(query.NewsSourceId)
            : response;
    }
}
