using VisaTelegramBot.Application.NewsSources;
using VisaTelegramBot.Application.NewsSources.Create;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Application.Tests.NewsSources;

public sealed class CreateNewsSourceCommandValidatorTests
{
    private readonly CreateNewsSourceCommandValidator _validator = new();

    [Fact]
    public void ValidRssCommand_Passes()
    {
        var result = _validator.Validate(new CreateNewsSourceCommand("Kaynak", "https://example.com/feed", SourceType.Rss, null, 15));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidHtmlCommand_Passes()
    {
        var rules = new HtmlParsingRulesDto("article", "h2", null, null, null);

        var result = _validator.Validate(new CreateNewsSourceCommand("Kaynak", "https://example.com", SourceType.Html, rules, 15));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "https://example.com/feed", SourceType.Rss, 15)]
    [InlineData("Kaynak", "not-a-url", SourceType.Rss, 15)]
    [InlineData("Kaynak", "https://example.com/feed", SourceType.Rss, 0)]
    [InlineData("Kaynak", "https://example.com/feed", SourceType.Html, 15)]
    [InlineData("Kaynak", "https://example.com/feed", (SourceType)42, 15)]
    public void InvalidCommand_Fails(string name, string url, SourceType type, int interval)
    {
        var result = _validator.Validate(new CreateNewsSourceCommand(name, url, type, null, interval));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RssWithParsingRules_Fails()
    {
        var rules = new HtmlParsingRulesDto("article", "h2", null, null, null);

        var result = _validator.Validate(new CreateNewsSourceCommand("Kaynak", "https://example.com", SourceType.Rss, rules, 15));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "ParsingRules");
    }
}
