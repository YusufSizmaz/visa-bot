using VisaTelegramBot.Application.Abstractions.Messaging;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.Abstractions.Publishing;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Domain.NewsItems;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Application.NewsItems.Delivery;

/// <summary>
/// Kuyruktaki tek bir haberi yayinlar ve sonucu kaydeder.
/// Garanti "en az bir kez" (at-least-once): mesaj gonderilip kayit yazilamadan process coker ise haber tekrar gonderilebilir.
/// "Tam olarak bir kez" dagitik sistemlerde karsi taraf idempotency desteklemedikce mumkun degildir.
/// </summary>
internal sealed class DeliverNewsItemCommandHandler(
    INewsItemRepository newsItemRepository,
    INewsSourceRepository newsSourceRepository,
    INewsPublisher publisher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<DeliverNewsItemCommand, DeliverNewsItemResult>
{
    private static readonly TimeSpan DefaultRateLimitDelay = TimeSpan.FromSeconds(5);

    public async Task<Result<DeliverNewsItemResult>> Handle(
        DeliverNewsItemCommand command,
        CancellationToken cancellationToken)
    {
        var item = await newsItemRepository.GetByIdAsync(command.NewsItemId, cancellationToken);

        if (item is null)
        {
            return NewsItemErrors.NotFound(command.NewsItemId);
        }

        if (item.DeliveryStatus != DeliveryStatus.Pending)
        {
            return new DeliverNewsItemResult(item.Id, DeliveryOutcome.AlreadyProcessed);
        }

        var source = await newsSourceRepository.GetByIdAsync(item.NewsSourceId, cancellationToken);

        var message = new NewsMessage(
            item.Id,
            item.Title,
            item.Summary,
            item.Url.Value,
            source?.Name ?? "Bilinmeyen kaynak",
            item.PublishedAtUtc,
            source?.Category ?? SourceCategory.Visa);

        var publishResult = await publisher.PublishAsync(message, cancellationToken);
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var outcome = Apply(item, publishResult, utcNow);

        // Mesaj artik gonderildi (ya da gonderilemedi). Iptal sinyali geldi diye bu bilgiyi kaybetmemek icin
        // kayit, iptal token'i olmadan yapilir. Aksi halde kapanis sirasinda ayni haber tekrar gonderilirdi.
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        return new DeliverNewsItemResult(item.Id, outcome, publishResult.RetryAfter);
    }

    private static DeliveryOutcome Apply(NewsItem item, PublishResult result, DateTime utcNow)
    {
        switch (result.Outcome)
        {
            case PublishOutcome.Published:
                item.MarkAsDelivered(result.ExternalMessageId, utcNow);
                return DeliveryOutcome.Delivered;

            case PublishOutcome.RateLimited:
                item.DeferDelivery(result.RetryAfter ?? DefaultRateLimitDelay, utcNow);
                return DeliveryOutcome.Deferred;

            case PublishOutcome.TransientFailure:
                item.RecordDeliveryFailure(result.Error ?? "Geçici hata.", isPermanent: false, utcNow);
                return item.DeliveryStatus == DeliveryStatus.Failed ? DeliveryOutcome.Failed : DeliveryOutcome.RetryScheduled;

            case PublishOutcome.PermanentFailure:
                item.RecordDeliveryFailure(result.Error ?? "Kalıcı hata.", isPermanent: true, utcNow);
                return DeliveryOutcome.Failed;

            default:
                throw new InvalidOperationException($"Bilinmeyen yayın sonucu: {result.Outcome}");
        }
    }
}
