using Microsoft.EntityFrameworkCore;
using VisaTelegramBot.Application.ChannelMessages;
using VisaTelegramBot.Application.FlightDeals;
using VisaTelegramBot.Domain.ChannelMessages;
using VisaTelegramBot.Domain.FlightDeals;

namespace VisaTelegramBot.Infrastructure.Persistence.Queries;

internal sealed class ChannelMessageQueries(AppDbContext dbContext) : IChannelMessageQueries
{
    public async Task<ChannelMessagePage> ListAsync(ChannelMessageListFilter filter, CancellationToken cancellationToken)
    {
        var query = dbContext.ChannelMessages.AsNoTracking();

        if (filter.Status is not null)
        {
            query = query.Where(message => message.Status == filter.Status);
        }

        var total = await query.CountAsync(cancellationToken);

        // Projeksiyon: gorsel baytlari (PhotoContent) SELECT'e hic girmez, sadece var mi yok mu bakilir.
        var rows = await query
            .OrderByDescending(message => message.Status == ChannelMessageStatus.Scheduled)
            .ThenByDescending(message => message.CreatedAtUtc)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(message => new
            {
                message.Id,
                message.Kind,
                message.Title,
                message.Body,
                message.LinkUrl,
                ButtonText = message.Button == null ? null : message.Button.Text,
                ButtonUrl = message.Button == null ? null : message.Button.Url,
                HasPhoto = message.Photo != null,
                message.Status,
                message.CreatedAtUtc,
                message.ScheduledAtUtc,
                message.SentAtUtc,
                message.Attempts,
                message.LastError
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new ChannelMessageResponse(
                row.Id,
                row.Kind,
                row.Title,
                row.Body,
                row.LinkUrl?.Value,
                row.ButtonText,
                row.ButtonUrl?.Value,
                row.HasPhoto,
                row.Status,
                row.CreatedAtUtc,
                row.ScheduledAtUtc,
                row.SentAtUtc,
                row.Attempts,
                row.LastError))
            .ToList();

        return new ChannelMessagePage(items, filter.Page, filter.PageSize, total);
    }

    public async Task<ChannelMessagePhotoResponse?> GetPhotoAsync(Guid id, CancellationToken cancellationToken)
    {
        var photo = await dbContext.ChannelMessages
            .AsNoTracking()
            .Where(message => message.Id == id && message.Photo != null)
            .Select(message => new { message.Photo!.Content, message.Photo.ContentType, message.Photo.FileName })
            .FirstOrDefaultAsync(cancellationToken);

        return photo is null ? null : new ChannelMessagePhotoResponse(photo.Content, photo.ContentType, photo.FileName);
    }

    public Task<int> CountScheduledAsync(CancellationToken cancellationToken) =>
        dbContext.ChannelMessages.CountAsync(message => message.Status == ChannelMessageStatus.Scheduled, cancellationToken);
}

internal sealed class FlightDealQueries(AppDbContext dbContext) : IFlightDealQueries
{
    public async Task<IReadOnlyList<FlightRouteResponse>> ListRoutesAsync(CancellationToken cancellationToken)
    {
        var routes = await dbContext.FlightRoutes.AsNoTracking().OrderBy(route => route.Label).ToListAsync(cancellationToken);

        return routes.Select(route => new FlightRouteResponse(
                route.Id,
                route.Origin.Value,
                route.Destination.Value,
                route.Label,
                route.MaxPrice,
                route.MonthsAhead,
                (int)route.CheckInterval.TotalMinutes,
                route.AutoPublish,
                route.IsActive,
                route.NextCheckAtUtc,
                route.LastCheckedAtUtc,
                route.LastError))
            .ToList();
    }

    public async Task<IReadOnlyList<FlightDealResponse>> ListDealsAsync(FlightDealStatus? status, int limit, CancellationToken cancellationToken)
    {
        var query = dbContext.FlightDeals.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(deal => deal.Status == status);
        }

        var rows = await query
            .Join(dbContext.FlightRoutes.AsNoTracking(), deal => deal.FlightRouteId, route => route.Id, (deal, route) => new { Deal = deal, route.Label })
            .OrderByDescending(row => row.Deal.FoundAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new FlightDealResponse(
                row.Deal.Id,
                row.Deal.FlightRouteId,
                row.Label,
                row.Deal.Origin.Value,
                row.Deal.Destination.Value,
                row.Deal.DepartureAt,
                row.Deal.Price,
                row.Deal.Currency,
                row.Deal.Airline,
                row.Deal.FlightNumber,
                row.Deal.Transfers,
                row.Deal.BookingUrl.Value,
                row.Deal.FoundAtUtc,
                row.Deal.Status,
                row.Deal.ChannelMessageId))
            .ToList();
    }
}
