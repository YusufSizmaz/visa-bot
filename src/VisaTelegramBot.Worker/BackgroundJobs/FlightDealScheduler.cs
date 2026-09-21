using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.Extensions.Options;
using VisaTelegramBot.Application.FlightDeals;

namespace VisaTelegramBot.Worker.BackgroundJobs;

public sealed class FlightDealSchedulerOptions
{
    public const string SectionName = "FlightDealScheduler";

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(1);

    [Range(1, 100)]
    public int DueRouteBatchSize { get; init; } = 10;
}

/// <summary>
/// Zamani gelen ucus rotalarini sirayla kontrol eder. Rota sayisi az ve fiyat API'si kotali oldugu icin
/// haberlerdeki paralel producer-consumer yerine basit ve sirali bir dongu yeterli.
/// </summary>
internal sealed class FlightDealScheduler(
    IServiceScopeFactory scopeFactory,
    IOptions<FlightDealSchedulerOptions> options,
    ILogger<FlightDealScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        using var timer = new PeriodicTimer(settings.PollInterval);

        try
        {
            do
            {
                await CheckDueRoutesAsync(settings.DueRouteBatchSize, stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal kapanis.
        }
    }

    private async Task CheckDueRoutesAsync(int batchSize, CancellationToken stoppingToken)
    {
        try
        {
            IReadOnlyList<Guid> dueIds;

            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var result = await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new GetDueFlightRouteIdsQuery(batchSize), stoppingToken);

                dueIds = result.IsSuccess ? result.Value : [];
            }

            foreach (var id in dueIds)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CheckFlightRouteCommand(id), stoppingToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Uçuş rotaları kontrol edilirken beklenmeyen hata.");
        }
    }
}
