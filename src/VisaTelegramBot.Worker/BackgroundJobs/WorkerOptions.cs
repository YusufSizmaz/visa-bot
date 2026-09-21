using System.ComponentModel.DataAnnotations;

namespace VisaTelegramBot.Worker.BackgroundJobs;

public sealed class FetchSchedulerOptions
{
    public const string SectionName = "FetchScheduler";

    /// <summary>Zamani gelen kaynaklarin ne siklikla sorgulanacagi.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Ayni anda cekilecek en fazla kaynak sayisi (tuketici sayisi).</summary>
    [Range(1, 64)]
    public int MaxConcurrentFetches { get; init; } = 4;

    /// <summary>Her sorguda alinacak en fazla kaynak.</summary>
    [Range(1, 1000)]
    public int DueSourceBatchSize { get; init; } = 100;

    /// <summary>
    /// Kuyruk kapasitesi. Doluysa uretici bekler (backpressure): tuketiciler yetisemezse bellekte sinirsiz is birikmez.
    /// </summary>
    [Range(1, 10000)]
    public int QueueCapacity { get; init; } = 200;
}

public sealed class DeliveryProcessorOptions
{
    public const string SectionName = "DeliveryProcessor";

    /// <summary>Kuyruk bosken ne siklikla kontrol edilecegi.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    [Range(1, 500)]
    public int BatchSize { get; init; } = 10;

    /// <summary>Kiralanan haberlerin baska worker'a kapali kalacagi sure. Bir partinin gonderimi bundan kisa surmeli.</summary>
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(5);
}
