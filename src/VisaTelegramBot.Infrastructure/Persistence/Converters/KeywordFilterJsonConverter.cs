using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Infrastructure.Persistence.Converters;

/// <summary>Anahtar kelime listesini tek bir JSON dizi kolonunda saklar.</summary>
internal sealed class KeywordFilterJsonConverter() : ValueConverter<KeywordFilter, string>(
    filter => Serialize(filter),
    json => Deserialize(json))
{
    public const int MaxLength = 4000;

    // Turkce karakterler ı gibi kacis dizilerine donusmesin, veritabaninda okunabilir kalsin.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string Serialize(KeywordFilter filter) => JsonSerializer.Serialize(filter.Keywords, JsonOptions);

    private static KeywordFilter Deserialize(string json)
    {
        var keywords = JsonSerializer.Deserialize<string[]>(json) ?? [];

        return KeywordFilter.CreateOrNull(keywords)
            ?? throw new InvalidOperationException("Kayıtlı anahtar kelime filtresi boş olamaz.");
    }
}
