using FluentValidation;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Domain.NewsItems;

namespace VisaTelegramBot.Application.NewsItems.Queries;

public sealed record ListNewsItemsQuery(
    Guid? NewsSourceId,
    DeliveryStatus? DeliveryStatus,
    string? Cursor,
    int PageSize = ListNewsItemsQuery.DefaultPageSize) : IQuery<CursorPage<NewsItemResponse>>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}

internal sealed class ListNewsItemsQueryValidator : AbstractValidator<ListNewsItemsQuery>
{
    public ListNewsItemsQueryValidator()
    {
        RuleFor(x => x.PageSize).InclusiveBetween(1, ListNewsItemsQuery.MaxPageSize);

        RuleFor(x => x.DeliveryStatus).IsInEnum().When(x => x.DeliveryStatus is not null);

        RuleFor(x => x.Cursor)
            .Must(cursor => NewsItemCursor.TryDecode(cursor, out _))
            .When(x => !string.IsNullOrEmpty(x.Cursor))
            .WithMessage("Geçersiz sayfa imleci.");
    }
}

internal sealed class ListNewsItemsQueryHandler(INewsItemQueries queries)
    : IQueryHandler<ListNewsItemsQuery, CursorPage<NewsItemResponse>>
{
    public async Task<Result<CursorPage<NewsItemResponse>>> Handle(
        ListNewsItemsQuery query,
        CancellationToken cancellationToken)
    {
        NewsItemCursor.TryDecode(query.Cursor, out var after);

        var page = await queries.ListAsync(
            new NewsItemListFilter(query.NewsSourceId, query.DeliveryStatus, after, query.PageSize),
            cancellationToken);

        return Result.Success(page);
    }
}
