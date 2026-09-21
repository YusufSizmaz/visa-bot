namespace VisaTelegramBot.Application.Abstractions.Scraping;

/// <summary>
/// Bir kaynaktan okunmus ham kayit. Henuz dogrulanmamistir; bos veya bozuk alanlar icerebilir.
/// </summary>
public sealed record FeedEntry(
    string? Title,
    string? Link,
    string? Summary,
    DateTime? PublishedAtUtc);
