using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Infrastructure.Persistence.Converters;

/// <summary>
/// Value object'i tek bir JSON kolonunda saklar. Okurken domain factory'si kullanilir, boylece kurallar yine uygulanir.
/// </summary>
internal sealed class HtmlParsingRulesJsonConverter() : ValueConverter<HtmlParsingRules, string>(
    rules => Serialize(rules),
    json => Deserialize(json))
{
    public const int MaxLength = 2000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string Serialize(HtmlParsingRules rules)
    {
        var document = new Document(
            rules.ItemSelector,
            rules.TitleSelector,
            rules.LinkSelector,
            rules.SummarySelector,
            rules.PublishedAtSelector);

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    private static HtmlParsingRules Deserialize(string json)
    {
        var document = JsonSerializer.Deserialize<Document>(json, JsonOptions)
            ?? throw new InvalidOperationException("Ayrıştırma kuralları okunamadı.");

        return HtmlParsingRules.Create(
            document.ItemSelector,
            document.TitleSelector,
            document.LinkSelector,
            document.SummarySelector,
            document.PublishedAtSelector);
    }

    private sealed record Document(
        string ItemSelector,
        string TitleSelector,
        string? LinkSelector,
        string? SummarySelector,
        string? PublishedAtSelector);
}
