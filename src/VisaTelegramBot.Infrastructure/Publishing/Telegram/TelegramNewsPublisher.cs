using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using VisaTelegramBot.Application.Abstractions.Publishing;

namespace VisaTelegramBot.Infrastructure.Publishing.Telegram;

/// <summary>
/// Adapter + Anti-Corruption Layer: Telegram.Bot kutuphanesinin tiplerini ve hatalarini
/// uygulamanin kendi diline (PublishResult) cevirir. Telegram tipleri bu sinifin disina cikmaz.
/// </summary>
internal sealed class TelegramNewsPublisher(
    ITelegramBotClient botClient,
    IOptions<TelegramOptions> options,
    ILogger<TelegramNewsPublisher> logger) : INewsPublisher
{
    public const string HttpClientName = "telegram";

    private const int TooManyRequests = 429;
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(5);

    public Task<PublishResult> PublishAsync(NewsMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        return SendSafelyAsync(
            () => botClient.SendMessage(
                chatId: TelegramChat.ToChatId(settings.ChannelId),
                text: TelegramMessageFormatter.Format(message),
                parseMode: ParseMode.Html,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = settings.DisableLinkPreview },
                cancellationToken: cancellationToken),
            cancellationToken);
    }

    public Task<PublishResult> PublishPostAsync(ChannelPost post, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var chatId = TelegramChat.ToChatId(settings.ChannelId);
        var markup = post.Button is null ? null : new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl(post.Button.Text, post.Button.Url));

        if (post.Photo is null)
        {
            return SendSafelyAsync(
                () => botClient.SendMessage(
                    chatId: chatId,
                    text: TelegramMessageFormatter.FormatPost(post, TelegramMessageFormatter.MaxMessageLength),
                    parseMode: ParseMode.Html,
                    replyMarkup: markup,
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = settings.DisableLinkPreview },
                    cancellationToken: cancellationToken),
                cancellationToken);
        }

        return SendSafelyAsync(
            async () =>
            {
                // Her denemede yeni stream: basarisiz bir istek stream'i sona kadar okumus olabilir.
                await using var stream = new MemoryStream(post.Photo.Content, writable: false);

                return await botClient.SendPhoto(
                    chatId: chatId,
                    photo: InputFile.FromStream(stream, post.Photo.FileName),
                    caption: TelegramMessageFormatter.FormatPost(post, TelegramMessageFormatter.MaxCaptionLength),
                    parseMode: ParseMode.Html,
                    replyMarkup: markup,
                    cancellationToken: cancellationToken);
            },
            cancellationToken);
    }

    /// <summary>Tum gonderim turleri icin ortak hata cevirisi. Tekrari onler ve davranisi tutarli kilar.</summary>
    private async Task<PublishResult> SendSafelyAsync(Func<Task<Message>> send, CancellationToken cancellationToken)
    {
        try
        {
            var sent = await send();
            return PublishResult.Published(sent.Id.ToString(CultureInfo.InvariantCulture));
        }
        catch (ApiRequestException exception) when (exception.ErrorCode == TooManyRequests)
        {
            var retryAfter = exception.Parameters?.RetryAfter is int seconds
                ? TimeSpan.FromSeconds(seconds)
                : DefaultRetryAfter;

            logger.LogWarning("Telegram hız limiti. {RetryAfterSeconds} sn beklenecek.", retryAfter.TotalSeconds);

            return PublishResult.RateLimited(retryAfter);
        }
        catch (ApiRequestException exception) when (IsConfigurationProblem(exception))
        {
            // Bot kanalda yetkisiz, kanal bulunamadi veya token gecersiz. Mesajin sucu yok;
            // ayar duzeltilince tekrar denenebilsin diye gecici hata sayilir. Stack trace bilgi vermez, mesaj yeterli.
            logger.LogError(
                "Telegram yapılandırma hatası ({ErrorCode}: {Message}). Token doğru mu, bot kanala yönetici olarak eklendi mi?",
                exception.ErrorCode,
                exception.Message);

            return PublishResult.TransientFailure($"Telegram {exception.ErrorCode}: {exception.Message}");
        }
        catch (ApiRequestException exception) when (exception.ErrorCode is >= 400 and < 500)
        {
            // Ornegin mesaj bicimi gecersiz. Ayni mesaji tekrar gondermek ayni hatayi verir.
            return PublishResult.PermanentFailure($"Telegram {exception.ErrorCode}: {exception.Message}");
        }
        catch (ApiRequestException exception)
        {
            return PublishResult.TransientFailure($"Telegram {exception.ErrorCode}: {exception.Message}");
        }
        catch (RequestException exception)
        {
            return PublishResult.TransientFailure($"Telegram'a ulaşılamadı: {exception.Message}");
        }
        catch (HttpRequestException exception)
        {
            return PublishResult.TransientFailure($"Telegram'a ulaşılamadı: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PublishResult.TransientFailure("Telegram isteği zaman aşımına uğradı.");
        }
    }

    private static bool IsConfigurationProblem(ApiRequestException exception)
    {
        return exception.ErrorCode is 401 or 403
            || (exception.ErrorCode == 400 && exception.Message.Contains("chat not found", StringComparison.OrdinalIgnoreCase));
    }
}

internal static class TelegramChat
{
    public static ChatId ToChatId(string channelId)
    {
        return long.TryParse(channelId, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var numericId)
            ? new ChatId(numericId)
            : new ChatId(channelId);
    }
}

/// <summary>Panel icin kanal adi ve abone sayisi.</summary>
internal sealed class TelegramChannelInfoProvider(
    ITelegramBotClient botClient,
    IOptions<TelegramOptions> options,
    ILogger<TelegramChannelInfoProvider> logger) : IChannelInfoProvider
{
    public async Task<ChannelInfo?> GetAsync(CancellationToken cancellationToken)
    {
        var chatId = TelegramChat.ToChatId(options.Value.ChannelId);

        try
        {
            var chat = await botClient.GetChat(chatId, cancellationToken);
            var members = await botClient.GetChatMemberCount(chatId, cancellationToken);

            return new ChannelInfo(chat.Title ?? chat.Username ?? options.Value.ChannelId, chat.Username, members);
        }
        catch (Exception exception) when (exception is RequestException or HttpRequestException)
        {
            logger.LogWarning("Kanal bilgisi alınamadı: {Message}", exception.Message);
            return null;
        }
    }
}

internal sealed class NullChannelInfoProvider : IChannelInfoProvider
{
    public Task<ChannelInfo?> GetAsync(CancellationToken cancellationToken) => Task.FromResult<ChannelInfo?>(null);
}
