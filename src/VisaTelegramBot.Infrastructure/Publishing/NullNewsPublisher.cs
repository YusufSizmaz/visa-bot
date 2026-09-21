using Microsoft.Extensions.Logging;
using VisaTelegramBot.Application.Abstractions.Publishing;

namespace VisaTelegramBot.Infrastructure.Publishing;

/// <summary>
/// Null Object deseni: yayinci yapilandirilmamis bir host'ta (ornegin Web API) "null" kontrolu yazmak yerine
/// hicbir sey yapmayan ama sozlesmeye uyan bir implementasyon kullanilir.
/// </summary>
internal sealed class NullNewsPublisher(ILogger<NullNewsPublisher> logger) : INewsPublisher
{
    public Task<PublishResult> PublishAsync(NewsMessage message, CancellationToken cancellationToken)
    {
        logger.LogWarning("Bu uygulamada yayıncı yapılandırılmamış; haber {NewsItemId} gönderilmedi.", message.NewsItemId);

        return Task.FromResult(PublishResult.TransientFailure("Yayıncı yapılandırılmamış."));
    }

    public Task<PublishResult> PublishPostAsync(ChannelPost post, CancellationToken cancellationToken)
    {
        logger.LogWarning("Bu uygulamada yayıncı yapılandırılmamış; mesaj {ChannelMessageId} gönderilmedi.", post.Id);

        return Task.FromResult(PublishResult.TransientFailure("Yayıncı yapılandırılmamış."));
    }
}
