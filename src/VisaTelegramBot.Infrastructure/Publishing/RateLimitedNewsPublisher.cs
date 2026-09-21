using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using VisaTelegramBot.Application.Abstractions.Publishing;
using VisaTelegramBot.Infrastructure.Publishing.Telegram;

namespace VisaTelegramBot.Infrastructure.Publishing;

/// <summary>
/// Decorator deseni: asil yayinciyi degistirmeden onune hiz limiti ekler.
/// TelegramNewsPublisher sadece mesaj gondermeyi bilir, bu sinif sadece hizi bilir (Single Responsibility).
/// </summary>
internal sealed class RateLimitedNewsPublisher(INewsPublisher inner, NewsPublishRateLimiter rateLimiter) : INewsPublisher
{
    public Task<PublishResult> PublishAsync(NewsMessage message, CancellationToken cancellationToken) =>
        WithRateLimitAsync(() => inner.PublishAsync(message, cancellationToken), cancellationToken);

    public Task<PublishResult> PublishPostAsync(ChannelPost post, CancellationToken cancellationToken) =>
        WithRateLimitAsync(() => inner.PublishPostAsync(post, cancellationToken), cancellationToken);

    private async Task<PublishResult> WithRateLimitAsync(Func<Task<PublishResult>> publish, CancellationToken cancellationToken)
    {
        using var lease = await rateLimiter.AcquireAsync(cancellationToken);

        if (!lease.IsAcquired)
        {
            var retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var value) ? value : TimeSpan.FromSeconds(5);
            return PublishResult.RateLimited(retryAfter);
        }

        return await publish();
    }
}

/// <summary>
/// Token Bucket algoritmasi. Kova her periyotta bir jetonla dolar, her mesaj bir jeton harcar.
/// Tek bir sayac oldugu icin singleton yasar; tum istekler ayni kovayi paylasir.
/// Not: sayac process icindedir. Birden fazla worker kopyasinda toplam hiz, kopya sayisiyla carpilir.
/// </summary>
internal sealed class NewsPublishRateLimiter : IAsyncDisposable
{
    private const int MaxQueuedMessages = 1000;

    private readonly TokenBucketRateLimiter _limiter;

    public NewsPublishRateLimiter(IOptions<TelegramOptions> options)
    {
        var messagesPerMinute = options.Value.MessagesPerMinute;

        _limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            // Kovada tek jeton: patlama (burst) yok, mesajlar esit aralikla gider.
            TokenLimit = 1,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1) / messagesPerMinute,
            QueueLimit = MaxQueuedMessages,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    }

    public ValueTask<RateLimitLease> AcquireAsync(CancellationToken cancellationToken) =>
        _limiter.AcquireAsync(permitCount: 1, cancellationToken);

    public ValueTask DisposeAsync() => _limiter.DisposeAsync();
}
