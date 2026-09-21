using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsItems;

namespace VisaTelegramBot.Domain.Tests.Common;

public sealed class ValueObjectTests
{
    [Theory]
    [InlineData("https://example.com/news")]
    [InlineData("http://example.com")]
    [InlineData("  https://example.com/a?b=c  ")]
    public void WebUrl_AcceptsAbsoluteHttpUrls(string value)
    {
        Assert.True(WebUrl.TryCreate(value, out _, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("example.com")]
    [InlineData("/relative/path")]
    [InlineData("ftp://example.com/file")]
    [InlineData("javascript:alert(1)")]
    public void WebUrl_RejectsInvalidValues(string? value)
    {
        Assert.False(WebUrl.TryCreate(value, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void WebUrl_EqualityIsStructural()
    {
        Assert.Equal(WebUrl.Create("https://EXAMPLE.com/a"), WebUrl.Create("https://example.com/a"));
    }

    [Theory]
    [InlineData("https://example.com/news/1", "http://example.com/news/1")]
    [InlineData("https://example.com/news/1", "https://www.example.com/news/1")]
    [InlineData("https://example.com/news/1", "https://example.com/news/1/")]
    [InlineData("https://example.com/news/1", "https://example.com/news/1?utm_source=telegram&utm_medium=social")]
    [InlineData("https://example.com/news/1", "https://example.com/news/1#comments")]
    [InlineData("https://example.com/news?id=5", "https://example.com/news?id=5&fbclid=abc")]
    public void ContentHash_IgnoresInsignificantUrlDifferences(string first, string second)
    {
        Assert.Equal(
            ContentHash.FromUrl(WebUrl.Create(first)),
            ContentHash.FromUrl(WebUrl.Create(second)));
    }

    [Theory]
    [InlineData("https://example.com/news/1", "https://example.com/news/2")]
    [InlineData("https://example.com/news?id=5", "https://example.com/news?id=6")]
    [InlineData("https://example.com/news/1", "https://other.com/news/1")]
    public void ContentHash_DistinguishesDifferentArticles(string first, string second)
    {
        Assert.NotEqual(
            ContentHash.FromUrl(WebUrl.Create(first)),
            ContentHash.FromUrl(WebUrl.Create(second)));
    }

    [Fact]
    public void ContentHash_FromValue_RoundTrips()
    {
        var hash = ContentHash.FromUrl(WebUrl.Create("https://example.com/a"));

        Assert.Equal(hash, ContentHash.FromValue(hash.Value));
        Assert.Equal(ContentHash.Length, hash.Value.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void ContentHash_FromValue_RejectsInvalid(string? value)
    {
        Assert.Throws<DomainException>(() => ContentHash.FromValue(value));
    }
}
