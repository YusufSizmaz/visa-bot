using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Domain.ChannelMessages;

/// <summary>
/// Kanala gonderilecek serbest icerikli mesaj. Aggregate root.
/// Haberlerle ayni outbox mantigini kullanir: once kaydedilir, zamani gelince Worker gonderir.
/// </summary>
public sealed class ChannelMessage : AggregateRoot<Guid>
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 3500;

    /// <summary>Telegram fotograf aciklamasini 1024 karakterle sinirlar; bicimlendirme payi birakilir.</summary>
    public const int PhotoCaptionMaxLength = 900;

    public const int ExternalMessageIdMaxLength = 100;
    public const int LastErrorMaxLength = 1000;

    private ChannelMessage()
    {
    }

    private ChannelMessage(Guid id, ChannelMessageKind kind, DateTime utcNow) : base(id)
    {
        Kind = kind;
        CreatedAtUtc = utcNow;
        Status = ChannelMessageStatus.Scheduled;
    }

    public ChannelMessageKind Kind { get; private set; }

    public string? Title { get; private set; }

    public string Body { get; private set; } = null!;

    public WebUrl? LinkUrl { get; private set; }

    public MessageButton? Button { get; private set; }

    public MessagePhoto? Photo { get; private set; }

    public ChannelMessageStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ScheduledAtUtc { get; private set; }

    public int Attempts { get; private set; }

    /// <summary>Birden fazla worker ayni mesaji ayni anda gondermesin diye kiralama suresi.</summary>
    public DateTime? LockedUntilUtc { get; private set; }

    public DateTime? SentAtUtc { get; private set; }

    public string? ExternalMessageId { get; private set; }

    public string? LastError { get; private set; }

    public static ChannelMessage Create(
        ChannelMessageKind kind,
        string? title,
        string body,
        WebUrl? linkUrl,
        MessageButton? button,
        MessagePhoto? photo,
        DateTime? scheduledAtUtc,
        DateTime utcNow)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new DomainException("Geçersiz mesaj türü.");
        }

        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        var normalizedBody = body?.Trim() ?? string.Empty;

        if (normalizedBody.Length == 0)
        {
            throw new DomainException("Mesaj metni boş olamaz.");
        }

        if (normalizedTitle?.Length > TitleMaxLength)
        {
            throw new DomainException($"Başlık en fazla {TitleMaxLength} karakter olabilir.");
        }

        if (normalizedBody.Length > BodyMaxLength)
        {
            throw new DomainException($"Mesaj metni en fazla {BodyMaxLength} karakter olabilir.");
        }

        if (photo is not null && (normalizedTitle?.Length ?? 0) + normalizedBody.Length > PhotoCaptionMaxLength)
        {
            throw new DomainException(
                $"Görselli mesajlarda başlık ve metin toplamı en fazla {PhotoCaptionMaxLength} karakter olabilir (Telegram sınırı).");
        }

        return new ChannelMessage(Guid.CreateVersion7(), kind, utcNow)
        {
            Title = normalizedTitle,
            Body = normalizedBody,
            LinkUrl = linkUrl,
            Button = button,
            Photo = photo,
            // Gecmis bir zaman secilirse mesaj hemen gonderilir.
            ScheduledAtUtc = scheduledAtUtc is { } scheduled && scheduled > utcNow ? scheduled : utcNow
        };
    }

    public bool IsDue(DateTime utcNow) => Status == ChannelMessageStatus.Scheduled && ScheduledAtUtc <= utcNow;

    public void Reschedule(DateTime scheduledAtUtc, DateTime utcNow)
    {
        EnsureScheduled();

        ScheduledAtUtc = scheduledAtUtc > utcNow ? scheduledAtUtc : utcNow;
        LockedUntilUtc = null;
    }

    public void SendNow(DateTime utcNow) => Reschedule(utcNow, utcNow);

    public void Cancel()
    {
        if (Status == ChannelMessageStatus.Cancelled)
        {
            return;
        }

        EnsureScheduled();

        Status = ChannelMessageStatus.Cancelled;
        LockedUntilUtc = null;
    }

    public void MarkAsSent(string? externalMessageId, DateTime utcNow)
    {
        if (Status == ChannelMessageStatus.Sent)
        {
            return;
        }

        EnsureScheduled();

        Status = ChannelMessageStatus.Sent;
        Attempts++;
        SentAtUtc = utcNow;
        ExternalMessageId = externalMessageId is null ? null : Truncate(externalMessageId, ExternalMessageIdMaxLength);
        LastError = null;
        LockedUntilUtc = null;
    }

    /// <summary>Hiz limiti gibi mesajdan kaynaklanmayan durumlarda erteler; deneme sayilmaz.</summary>
    public void Defer(TimeSpan delay, DateTime utcNow)
    {
        EnsureScheduled();

        ScheduledAtUtc = utcNow + (delay < TimeSpan.Zero ? TimeSpan.Zero : delay);
        LockedUntilUtc = null;
    }

    public void RecordFailure(string error, bool isPermanent, DateTime utcNow)
    {
        if (Status != ChannelMessageStatus.Scheduled)
        {
            return;
        }

        Attempts++;
        LastError = Truncate(error, LastErrorMaxLength);
        LockedUntilUtc = null;

        if (!isPermanent && Attempts < RetryPolicy.MaxAttempts)
        {
            ScheduledAtUtc = utcNow + RetryPolicy.DelayFor(Attempts);
            return;
        }

        Status = ChannelMessageStatus.Failed;
    }

    /// <summary>Basarisiz mesaji tekrar gonderim sirasina alir.</summary>
    public void Retry(DateTime utcNow)
    {
        if (Status != ChannelMessageStatus.Failed)
        {
            throw new DomainException("Sadece başarısız mesajlar yeniden denenebilir.");
        }

        Status = ChannelMessageStatus.Scheduled;
        Attempts = 0;
        ScheduledAtUtc = utcNow;
        LastError = null;
        LockedUntilUtc = null;
    }

    private void EnsureScheduled()
    {
        if (Status != ChannelMessageStatus.Scheduled)
        {
            throw new DomainException($"Bu işlem sadece gönderim bekleyen mesajlarda yapılabilir. Mevcut durum: {Status}.");
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
