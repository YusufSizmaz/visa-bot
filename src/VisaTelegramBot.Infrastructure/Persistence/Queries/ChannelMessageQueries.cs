using Microsoft.EntityFrameworkCore;
using VisaTelegramBot.Application.ChannelMessages;
using VisaTelegramBot.Domain.ChannelMessages;

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
