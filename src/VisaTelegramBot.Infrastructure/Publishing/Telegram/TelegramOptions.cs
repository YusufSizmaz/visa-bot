using System.ComponentModel.DataAnnotations;

namespace VisaTelegramBot.Infrastructure.Publishing.Telegram;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    /// <summary>BotFather'dan alinan token. Ornek bicim: 123456789:AAH...</summary>
    [Required(ErrorMessage = "Telegram:BotToken zorunlu. BotFather'dan alınan token'ı user-secrets veya ortam değişkeni ile verin.")]
    [RegularExpression(@"^\d+:[A-Za-z0-9_-]{20,}$", ErrorMessage = "Telegram:BotToken biçimi geçersiz.")]
    public string BotToken { get; init; } = string.Empty;

    /// <summary>Kanal kullanici adi (@kanal_adi) veya sayisal kimlik (-1001234567890).</summary>
    [Required(ErrorMessage = "Telegram:ChannelId zorunlu. Örnek: @vize_haberleri veya -1001234567890")]
    [RegularExpression(@"^(@[A-Za-z0-9_]{4,}|-?\d+)$", ErrorMessage = "Telegram:ChannelId biçimi geçersiz.")]
    public string ChannelId { get; init; } = string.Empty;

    /// <summary>Telegram gruplara dakikada yaklasik 20 mesaja izin verir. Guvenli tarafta kaliriz.</summary>
    [Range(1, 30)]
    public int MessagesPerMinute { get; init; } = 20;

    public bool DisableLinkPreview { get; init; }
}
