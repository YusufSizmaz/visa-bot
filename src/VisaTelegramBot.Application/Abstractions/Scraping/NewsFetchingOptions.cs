using System.ComponentModel.DataAnnotations;

namespace VisaTelegramBot.Application.Abstractions.Scraping;

/// <summary>
/// Options deseni: ayarlar appsettings.json'dan tipli bir sinifa baglanir ve baslangicta dogrulanir.
/// </summary>
public sealed class NewsFetchingOptions
{
    public const string SectionName = "NewsFetching";

    /// <summary>
    /// Tek cekimde kanala gonderilmek uzere kuyruga alinacak en fazla haber. Fazlasi kaydedilir ama arsivlenir.
    /// Bir kaynak bozulup yuzlerce kaydi "yeni" gibi gosterirse kanal bu sayede bogulmaz.
    /// </summary>
    [Range(1, 500)]
    public int MaxQueuedItemsPerFetch { get; init; } = 20;

    /// <summary>Yayin tarihi bundan eski haberler kanala gonderilmez, arsivlenir.</summary>
    public TimeSpan MaxItemAge { get; init; } = TimeSpan.FromDays(3);
}
