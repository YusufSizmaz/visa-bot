using System.ComponentModel.DataAnnotations;

namespace VisaTelegramBot.Infrastructure.Scraping;

public sealed class ScrapingOptions
{
    public const string SectionName = "Scraping";

    /// <summary>
    /// Siteye kim oldugumuzu durustce bildiririz. "Mozilla/5.0 (compatible; BotAdi/Surum; +adres)" bicimi
    /// arama motoru botlarinin da kullandigi standarttir; bazi guvenlik duvarlari bu bicimde olmayan istekleri reddeder.
    /// </summary>
    [Required]
    public string UserAgent { get; init; } = "Mozilla/5.0 (compatible; VisaTelegramBot/1.0)";

    /// <summary>Tek bir yanitin en buyuk boyutu. Hatali veya kotu niyetli bir kaynak bellegi dolduramasin.</summary>
    [Range(64 * 1024, 50 * 1024 * 1024)]
    public int MaxResponseBytes { get; init; } = 5 * 1024 * 1024;

    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(15);

    public TimeSpan TotalTimeout { get; init; } = TimeSpan.FromSeconds(60);
}
