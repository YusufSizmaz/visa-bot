using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Domain.ChannelMessages;

namespace VisaTelegramBot.Application.ChannelMessages;

public sealed record ChannelMessageResponse(
    Guid Id,
    ChannelMessageKind Kind,
    string? Title,
    string Body,
    string? LinkUrl,
    string? ButtonText,
    string? ButtonUrl,
    bool HasPhoto,
    ChannelMessageStatus Status,
    DateTime CreatedAtUtc,
    DateTime ScheduledAtUtc,
    DateTime? SentAtUtc,
    int Attempts,
    string? LastError);

public sealed record ChannelMessagePhotoResponse(byte[] Content, string ContentType, string FileName);

public sealed record ChannelMessageListFilter(ChannelMessageStatus? Status, int Page, int PageSize);

public sealed record ChannelMessagePage(IReadOnlyList<ChannelMessageResponse> Items, int Page, int PageSize, int TotalCount);

public interface IChannelMessageQueries
{
    /// <remarks>Gorsel baytlari listeye dahil edilmez; ayri uc noktadan istenir.</remarks>
    Task<ChannelMessagePage> ListAsync(ChannelMessageListFilter filter, CancellationToken cancellationToken);

    Task<ChannelMessagePhotoResponse?> GetPhotoAsync(Guid id, CancellationToken cancellationToken);

    Task<int> CountScheduledAsync(CancellationToken cancellationToken);
}

public static class ChannelMessageErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("ChannelMessage.NotFound", $"'{id}' kimlikli mesaj bulunamadı.");

    public static Error PhotoNotFound(Guid id) =>
        Error.NotFound("ChannelMessage.PhotoNotFound", $"'{id}' kimlikli mesajın görseli yok.");

    public static Error InvalidState(string message) =>
        Error.Validation("ChannelMessage.InvalidState", message);
}
