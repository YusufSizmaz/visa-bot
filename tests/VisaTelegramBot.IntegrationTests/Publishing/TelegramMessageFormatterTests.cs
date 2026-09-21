using VisaTelegramBot.Application.Abstractions.Publishing;
using VisaTelegramBot.Infrastructure.Publishing.Telegram;

namespace VisaTelegramBot.IntegrationTests.Publishing;

public sealed class TelegramMessageFormatterTests
{
    [Fact]
    public void Format_EscapesHtmlFromSource()
    {
        var message = new NewsMessage(
            Guid.NewGuid(),
            "<script>alert(1)</script> & vize",
            "a < b > c",
            "https://example.com/a?x=1&y=\"2\"",
            "Kaynak <b>",
            new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc));

        var text = TelegramMessageFormatter.Format(message);

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt; &amp; vize", text);
        Assert.Contains("a &lt; b &gt; c", text);
        Assert.Contains("href=\"https://example.com/a?x=1&amp;y=&quot;2&quot;\"", text);
        Assert.Contains("10.09.2026", text);
        Assert.DoesNotContain("<script>", text);
    }

    [Fact]
    public void Format_FlightCampaign_UsesPlaneIconAndCampaignLinkText()
    {
        var message = new NewsMessage(
            Guid.NewGuid(), "AJet İstanbul-Rotterdam uçuşları başlıyor", null, "https://example.com/k", "Enuygun", null,
            Domain.NewsSources.SourceCategory.FlightCampaign);

        var text = TelegramMessageFormatter.Format(message);

        Assert.StartsWith("✈️ <b>", text);
        Assert.Contains(">Kampanyayı gör</a>", text);
    }

    [Fact]
    public void Format_NeverExceedsTelegramLimit()
    {
        var message = new NewsMessage(
            Guid.NewGuid(),
            new string('B', 5000),
            new string('S', 5000),
            "https://example.com/a",
            "Kaynak",
            null);

        var text = TelegramMessageFormatter.Format(message);

        Assert.True(text.Length <= TelegramMessageFormatter.MaxMessageLength);
    }
}
