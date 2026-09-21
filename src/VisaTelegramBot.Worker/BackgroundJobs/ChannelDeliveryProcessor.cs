using MediatR;
using Microsoft.Extensions.Options;
using VisaTelegramBot.Application.ChannelMessages;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Application.NewsItems.Delivery;

namespace VisaTelegramBot.Worker.BackgroundJobs;

/// <summary>
/// Outbox isleyicisi: kanala gidecek her seyi (ozel mesajlar ve haberler) kiralar ve tek tek yayinlar.
/// Once ozel mesajlar gonderilir; yoneticinin elle yazdigi duyuru, otomatik haberlerin arkasinda beklemesin.
/// Birden fazla Worker kopyasi calisabilir; kiralama sayesinde ayni kaydi iki kopya birlikte almaz.
/// </summary>
internal sealed class ChannelDeliveryProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<DeliveryProcessorOptions> options,
    ILogger<ChannelDeliveryProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        logger.LogInformation("Gönderim işleyicisi başladı. Parti boyutu: {BatchSize}", settings.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messages = await SendAsync(new ClaimDueChannelMessagesCommand(settings.BatchSize, settings.LeaseDuration), stoppingToken);

                foreach (var id in messages)
                {
                    await DeliverAsync(new DeliverChannelMessageCommand(id), id, stoppingToken);
                }

                var newsItems = await SendAsync(new ClaimDeliverableNewsItemsCommand(settings.BatchSize, settings.LeaseDuration), stoppingToken);

                foreach (var id in newsItems)
                {
                    await DeliverAsync(new DeliverNewsItemCommand(id), id, stoppingToken);
                }

                if (messages.Count == 0 && newsItems.Count == 0)
                {
                    await Task.Delay(settings.PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Gönderim döngüsünde beklenmeyen hata.");
                await DelaySafelyAsync(settings.PollInterval, stoppingToken);
            }
        }

        logger.LogInformation("Gönderim işleyicisi durdu.");
    }

    private async Task<IReadOnlyList<Guid>> SendAsync(IRequest<Result<IReadOnlyList<Guid>>> command, CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(command, stoppingToken);

        return result.IsSuccess ? result.Value : [];
    }

    private async Task DeliverAsync(IRequest<Result<DeliverNewsItemResult>> command, Guid id, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var result = await sender.Send(command, stoppingToken);

            if (result.IsSuccess && result.Value is { Outcome: DeliveryOutcome.Deferred, RetryAfter: { } retryAfter })
            {
                // Telegram "yavasla" dedi. Kalan kayitlari hemen denersek hepsi reddedilir.
                logger.LogWarning("Hız limiti nedeniyle {Seconds} sn bekleniyor.", retryAfter.TotalSeconds);
                await Task.Delay(retryAfter, stoppingToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Tek kaydin hatasi partideki digerlerini durdurmaz. Kira dolunca kayit tekrar alinir.
            logger.LogError(exception, "Kayıt {Id} gönderilirken beklenmeyen hata.", id);
        }
    }

    private static async Task DelaySafelyAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Kapanis sirasinda bekleme iptal edildi.
        }
    }
}
