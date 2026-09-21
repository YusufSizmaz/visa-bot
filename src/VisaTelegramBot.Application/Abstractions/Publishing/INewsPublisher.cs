namespace VisaTelegramBot.Application.Abstractions.Publishing;

/// <summary>
/// Haberi dis dunyaya (Telegram kanali) yayinlar. Application "Telegram" kelimesini bilmez.
/// Yarin Discord veya e-posta eklemek icin yeni bir implementasyon yazmak yeterlidir (Dependency Inversion).
/// </summary>
public interface INewsPublisher
{
    /// <remarks>Beklenen hatalari exception yerine <see cref="PublishResult"/> olarak doner.</remarks>
    Task<PublishResult> PublishAsync(NewsMessage message, CancellationToken cancellationToken);

    /// <summary>Yoneticinin yazdigi veya sistemin urettigi serbest icerikli gonderiyi yayinlar.</summary>
    Task<PublishResult> PublishPostAsync(ChannelPost post, CancellationToken cancellationToken);
}

public sealed record NewsMessage(
    Guid NewsItemId,
    string Title,
    string? Summary,
    string Url,
    string SourceName,
    DateTime? PublishedAtUtc,
    Domain.NewsSources.SourceCategory Category = Domain.NewsSources.SourceCategory.Visa);

public enum PublishOutcome
{
    Published = 1,

    /// <summary>Hiz limitine takildi. Haberde sorun yok, sadece beklemek gerekiyor.</summary>
    RateLimited = 2,

    /// <summary>Gecici hata (ag, 5xx). Tekrar denenebilir.</summary>
    TransientFailure = 3,

    /// <summary>Kalici hata (bot kanalda yetkisiz, mesaj formati gecersiz). Tekrar denemek anlamsiz.</summary>
    PermanentFailure = 4
}

public sealed record PublishResult(
    PublishOutcome Outcome,
    string? ExternalMessageId = null,
    TimeSpan? RetryAfter = null,
    string? Error = null)
{
    public static PublishResult Published(string? externalMessageId) => new(PublishOutcome.Published, externalMessageId);

    public static PublishResult RateLimited(TimeSpan retryAfter) =>
        new(PublishOutcome.RateLimited, RetryAfter: retryAfter, Error: "Hız limitine takıldı.");

    public static PublishResult TransientFailure(string error) => new(PublishOutcome.TransientFailure, Error: error);

    public static PublishResult PermanentFailure(string error) => new(PublishOutcome.PermanentFailure, Error: error);
}
