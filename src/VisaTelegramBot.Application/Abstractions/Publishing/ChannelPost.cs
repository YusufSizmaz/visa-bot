namespace VisaTelegramBot.Application.Abstractions.Publishing;

/// <summary>Kanala gonderilecek serbest icerikli gonderi. Telegram'a ozgu hicbir tip icermez.</summary>
public sealed record ChannelPost(
    Guid Id,
    string? Title,
    string Body,
    string? LinkUrl,
    PostButton? Button,
    PostPhoto? Photo);

public sealed record PostButton(string Text, string Url);

public sealed record PostPhoto(byte[] Content, string ContentType, string FileName);

/// <summary>Kanalin panelde gosterilecek ozet bilgisi.</summary>
public sealed record ChannelInfo(string Title, string? Username, int? MemberCount);

public interface IChannelInfoProvider
{
    /// <summary>Yayinci yapilandirilmamissa veya kanal okunamazsa null doner.</summary>
    Task<ChannelInfo?> GetAsync(CancellationToken cancellationToken);
}
