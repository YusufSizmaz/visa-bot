using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using VisaTelegramBot.Application.NewsSources;
using VisaTelegramBot.Application.NewsSources.Create;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.WebApi.Seed;

/// <summary>
/// Acilista bir JSON dosyasindaki kaynaklari ekler. Idempotent: ayni adres zaten kayitliysa atlanir,
/// boylece uygulama her yeniden basladiginda guvenle calisir ve yoneticinin yaptigi degisiklikleri ezmez.
/// Kaynaklar dogrudan veritabanina degil, normal CreateNewsSourceCommand uzerinden eklenir;
/// yani ayni dogrulama ve domain kurallarindan gecer.
/// </summary>
internal static class NewsSourceSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task SeedNewsSourcesAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        var relativePath = app.Configuration["Seeding:NewsSourcesFile"];

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(NewsSourceSeeder));
        var path = Path.Combine(app.Environment.ContentRootPath, relativePath);

        if (!File.Exists(path))
        {
            logger.LogWarning("Kaynak tohum dosyası bulunamadı: {Path}", path);
            return;
        }

        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<SeedDocument>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Tohum dosyası okunamadı: {path}");

        await using var scope = app.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        int created = 0, skipped = 0;

        foreach (var source in document.Sources)
        {
            var keywords = source.Keywords
                ?? (source.KeywordSet is not null && document.KeywordSets.TryGetValue(source.KeywordSet, out var set) ? set : null);

            var result = await sender.Send(
                new CreateNewsSourceCommand(source.Name, source.Url, source.Type, source.ParsingRules, source.FetchIntervalMinutes, keywords, source.TurkishOnly, source.Category ?? SourceCategory.Visa),
                cancellationToken);

            if (result.IsSuccess)
            {
                created++;
            }
            else if (result.Error == NewsSourceErrors.UrlAlreadyExists)
            {
                skipped++;
            }
            else
            {
                logger.LogError("Tohum kaynağı eklenemedi: {Name} - {Error}", source.Name, result.Error.Message);
            }
        }

        logger.LogInformation("Kaynak tohumlama tamamlandı: {Created} eklendi, {Skipped} zaten vardı.", created, skipped);
    }

    private sealed record SeedDocument(
        Dictionary<string, string[]> KeywordSets,
        List<SeedSource> Sources);

    private sealed record SeedSource(
        string Name,
        string Url,
        SourceType Type,
        int FetchIntervalMinutes,
        HtmlParsingRulesDto? ParsingRules,
        string? KeywordSet,
        string[]? Keywords,
        bool TurkishOnly,
        SourceCategory? Category);
}
