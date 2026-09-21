using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Domain.ChannelMessages;

/// <summary>Mesaja eklenen gorsel. Sadece JPEG, PNG ve WebP kabul edilir; tur, dosyanin ilk baytlarindan dogrulanir.</summary>
public sealed class MessagePhoto : ValueObject
{
    public const int MaxSizeBytes = 5 * 1024 * 1024;
    public const int FileNameMaxLength = 200;

    private MessagePhoto(byte[] content, string contentType, string fileName)
    {
        Content = content;
        ContentType = contentType;
        FileName = fileName;
    }

    // EF Core icin.
    private MessagePhoto()
    {
        Content = [];
        ContentType = string.Empty;
        FileName = string.Empty;
    }

    public byte[] Content { get; private set; }

    public string ContentType { get; private set; }

    public string FileName { get; private set; }

    public static MessagePhoto Create(byte[] content, string? fileName)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length == 0)
        {
            throw new DomainException("Görsel dosyası boş.");
        }

        if (content.Length > MaxSizeBytes)
        {
            throw new DomainException($"Görsel en fazla {MaxSizeBytes / (1024 * 1024)} MB olabilir.");
        }

        // Uzantiya veya istemcinin soyledigi Content-Type'a guvenilmez; dosyanin imzasi okunur.
        var contentType = DetectContentType(content)
            ?? throw new DomainException("Sadece JPEG, PNG veya WebP görseller kabul edilir.");

        var safeName = Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? "gorsel" : fileName.Trim());

        if (safeName.Length > FileNameMaxLength)
        {
            safeName = safeName[^FileNameMaxLength..];
        }

        return new MessagePhoto(content, contentType, safeName);
    }

    private static string? DetectContentType(byte[] content)
    {
        ReadOnlySpan<byte> bytes = content;

        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }

        if (bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        if (bytes.Length >= 12
            && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ContentType;
        yield return FileName;
        yield return Content.Length;
        yield return Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Content));
    }
}
