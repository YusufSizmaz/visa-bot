using System.Collections.Concurrent;
using System.Threading.Channels;
using MediatR;
using Microsoft.Extensions.Options;
using VisaTelegramBot.Application.NewsSources.Fetch;
using VisaTelegramBot.Application.NewsSources.Queries;

namespace VisaTelegramBot.Worker.BackgroundJobs;

/// <summary>
/// Producer-Consumer deseni:
/// - Uretici (producer) periyodik olarak zamani gelen kaynaklari bulur ve bir kanala (Channel) yazar.
/// - N adet tuketici (consumer) kanaldan okuyup kaynaklari paralel ceker.
/// Kanal sinirli (bounded) oldugu icin tuketiciler yavaslarsa uretici de yavaslar (backpressure).
/// </summary>
internal sealed class NewsFetchScheduler(
    IServiceScopeFactory scopeFactory,
    IOptions<FetchSchedulerOptions> options,
    ILogger<NewsFetchScheduler> logger) : BackgroundService
{
    // Hala islenmekte olan kaynak bir sonraki turda tekrar kuyruga eklenmesin.
    private readonly ConcurrentDictionary<Guid, byte> _inFlight = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        var channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(settings.QueueCapacity)
        {
            SingleWriter = true,
            SingleReader = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        logger.LogInformation(
            "Çekim zamanlayıcısı başladı. Aralık: {PollInterval}, eş zamanlı çekim: {Concurrency}",
            settings.PollInterval,
            settings.MaxConcurrentFetches);

        var consumers = Enumerable
            .Range(0, settings.MaxConcurrentFetches)
            .Select(_ => ConsumeAsync(channel.Reader, stoppingToken))
            .ToArray();

        await ProduceAsync(channel.Writer, settings, stoppingToken);
        await Task.WhenAll(consumers);

        logger.LogInformation("Çekim zamanlayıcısı durdu.");
    }

    private async Task ProduceAsync(ChannelWriter<Guid> writer, FetchSchedulerOptions settings, CancellationToken stoppingToken)
    {
        // PeriodicTimer: Task.Delay dongusunden farkli olarak isin suresi araligi kaydirmaz.
        using var timer = new PeriodicTimer(settings.PollInterval);

        try
        {
            do
            {
                await EnqueueDueSourcesAsync(writer, settings.DueSourceBatchSize, stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal kapanis.
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task EnqueueDueSourcesAsync(ChannelWriter<Guid> writer, int batchSize, CancellationToken stoppingToken)
    {
        try
        {
            IReadOnlyList<Guid> dueIds;

            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var result = await sender.Send(new GetDueNewsSourceIdsQuery(batchSize), stoppingToken);

                if (result.IsFailure)
                {
                    return;
                }

                dueIds = result.Value;
            }

            foreach (var id in dueIds)
            {
                if (_inFlight.TryAdd(id, 0))
                {
                    await writer.WriteAsync(id, stoppingToken);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Veritabani gecici olarak erisilemez olabilir. Zamanlayici olmemeli, bir sonraki turda tekrar dener.
            logger.LogError(exception, "Zamanı gelen kaynaklar alınamadı.");
        }
    }

    private async Task ConsumeAsync(ChannelReader<Guid> reader, CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var newsSourceId in reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    // Her is kendi DI scope'unda: DbContext gibi scoped servisler isler arasinda paylasilmaz.
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var sender = scope.ServiceProvider.GetRequiredService<ISender>();

                    await sender.Send(new FetchNewsSourceCommand(newsSourceId), stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Kaynak {NewsSourceId} çekilirken beklenmeyen hata.", newsSourceId);
                }
                finally
                {
                    _inFlight.TryRemove(newsSourceId, out _);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal kapanis.
        }
    }
}
