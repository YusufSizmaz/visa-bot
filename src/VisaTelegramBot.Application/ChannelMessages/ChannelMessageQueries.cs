using FluentValidation;
using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Abstractions.Publishing;
using VisaTelegramBot.Application.Abstractions.Caching;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Domain.ChannelMessages;

namespace VisaTelegramBot.Application.ChannelMessages;

public sealed record ListChannelMessagesQuery(ChannelMessageStatus? Status, int Page = 1, int PageSize = 20) : IQuery<ChannelMessagePage>;

internal sealed class ListChannelMessagesQueryValidator : AbstractValidator<ListChannelMessagesQuery>
{
    public ListChannelMessagesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
    }
}

/// <remarks>
/// Mesaj sayisi az olacagi icin burada klasik sayfa numarali (offset) sayfalama yeterli ve panelde daha kullanisli.
/// Haberlerdeki cursor sayfalamayla karsilastirinca iki yaklasimin ne zaman secilecegini gosterir.
/// </remarks>
internal sealed class ListChannelMessagesQueryHandler(IChannelMessageQueries queries)
    : IQueryHandler<ListChannelMessagesQuery, ChannelMessagePage>
{
    public async Task<Result<ChannelMessagePage>> Handle(ListChannelMessagesQuery query, CancellationToken cancellationToken)
    {
        var page = await queries.ListAsync(new ChannelMessageListFilter(query.Status, query.Page, query.PageSize), cancellationToken);

        return Result.Success(page);
    }
}

public sealed record GetChannelMessagePhotoQuery(Guid ChannelMessageId) : IQuery<ChannelMessagePhotoResponse>;

internal sealed class GetChannelMessagePhotoQueryHandler(IChannelMessageQueries queries)
    : IQueryHandler<GetChannelMessagePhotoQuery, ChannelMessagePhotoResponse>
{
    public async Task<Result<ChannelMessagePhotoResponse>> Handle(GetChannelMessagePhotoQuery query, CancellationToken cancellationToken)
    {
        var photo = await queries.GetPhotoAsync(query.ChannelMessageId, cancellationToken);

        return photo is null ? ChannelMessageErrors.PhotoNotFound(query.ChannelMessageId) : photo;
    }
}

public sealed record ChannelOverviewResponse(ChannelInfo? Channel, int ScheduledMessages);

public sealed record GetChannelOverviewQuery : IQuery<ChannelOverviewResponse>;

internal sealed class GetChannelOverviewQueryHandler(
    IChannelInfoProvider channelInfoProvider,
    IChannelMessageQueries queries,
    ICacheService cache) : IQueryHandler<GetChannelOverviewQuery, ChannelOverviewResponse>
{
    public async Task<Result<ChannelOverviewResponse>> Handle(GetChannelOverviewQuery query, CancellationToken cancellationToken)
    {
        // Abone sayisi Telegram'dan gelir; her panel yenilemesinde Telegram'a gitmemek icin kisa sure cache'lenir.
        var channel = await cache.GetOrCreateAsync(
            "channel:info",
            channelInfoProvider.GetAsync,
            TimeSpan.FromMinutes(2),
            tags: null,
            cancellationToken);

        var scheduled = await queries.CountScheduledAsync(cancellationToken);

        return new ChannelOverviewResponse(channel, scheduled);
    }
}
