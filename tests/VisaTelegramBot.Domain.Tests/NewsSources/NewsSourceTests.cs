using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsSources;
using VisaTelegramBot.Domain.NewsSources.Events;

namespace VisaTelegramBot.Domain.Tests.NewsSources;

public sealed class NewsSourceTests
{
    [Fact]
    public void Create_WithValidData_StartsActiveAndDueImmediately()
    {
        var source = TestData.RssSource();

        Assert.True(source.IsActive);
        Assert.True(source.HasNeverSucceeded);
        Assert.True(source.IsDueForFetch(TestData.Now));
        Assert.NotEqual(Guid.Empty, source.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_Throws(string name)
    {
        Assert.Throws<DomainException>(() => NewsSource.Create(
            name, WebUrl.Create("https://example.com"), SourceType.Rss, null, TimeSpan.FromMinutes(5), TestData.Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(24 * 60 + 1)]
    public void Create_WithIntervalOutOfRange_Throws(int minutes)
    {
        Assert.Throws<DomainException>(() => TestData.RssSource(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void Create_HtmlSourceWithoutRules_Throws()
    {
        Assert.Throws<DomainException>(() => NewsSource.Create(
            "Site", WebUrl.Create("https://example.com"), SourceType.Html, null, TimeSpan.FromMinutes(5), TestData.Now));
    }

    [Fact]
    public void Create_RssSourceWithRules_Throws()
    {
        var rules = HtmlParsingRules.Create("article", "h2");

        Assert.Throws<DomainException>(() => NewsSource.Create(
            "Site", WebUrl.Create("https://example.com"), SourceType.Rss, rules, TimeSpan.FromMinutes(5), TestData.Now));
    }

    [Fact]
    public void Create_WithUndefinedEnumValue_Throws()
    {
        Assert.Throws<DomainException>(() => NewsSource.Create(
            "Site", WebUrl.Create("https://example.com"), (SourceType)99, null, TimeSpan.FromMinutes(5), TestData.Now));
    }

    [Fact]
    public void RecordFetchSuccess_SchedulesNextFetchAfterInterval()
    {
        var source = TestData.RssSource(TimeSpan.FromMinutes(10));

        source.RecordFetchSuccess(TestData.Now);

        Assert.False(source.HasNeverSucceeded);
        Assert.False(source.IsDueForFetch(TestData.Now.AddMinutes(9)));
        Assert.True(source.IsDueForFetch(TestData.Now.AddMinutes(10)));
    }

    [Fact]
    public void RecordFetchFailure_ReachingLimit_DeactivatesAndRaisesEventOnce()
    {
        var source = TestData.RssSource();

        for (var i = 0; i < NewsSource.MaxConsecutiveFailures + 3; i++)
        {
            source.RecordFetchFailure("HTTP 500", TestData.Now.AddMinutes(i));
        }

        Assert.False(source.IsActive);
        Assert.Equal(NewsSource.MaxConsecutiveFailures, source.ConsecutiveFailureCount);
        Assert.Single(source.DomainEvents.OfType<NewsSourceDeactivatedDomainEvent>());
    }

    [Fact]
    public void RecordFetchSuccess_ResetsFailureCounter()
    {
        var source = TestData.RssSource();
        source.RecordFetchFailure("timeout", TestData.Now);
        source.RecordFetchFailure("timeout", TestData.Now);

        source.RecordFetchSuccess(TestData.Now);

        Assert.Equal(0, source.ConsecutiveFailureCount);
        Assert.Null(source.LastFetchError);
    }

    [Fact]
    public void Activate_AfterAutoDeactivation_ResetsStateAndIsDue()
    {
        var source = TestData.RssSource();

        for (var i = 0; i < NewsSource.MaxConsecutiveFailures; i++)
        {
            source.RecordFetchFailure("HTTP 500", TestData.Now);
        }

        var later = TestData.Now.AddHours(1);
        source.Activate(later);

        Assert.True(source.IsActive);
        Assert.Equal(0, source.ConsecutiveFailureCount);
        Assert.True(source.IsDueForFetch(later));
    }

    [Fact]
    public void Deactivate_MakesSourceNeverDue()
    {
        var source = TestData.RssSource();

        source.Deactivate();

        Assert.False(source.IsDueForFetch(TestData.Now.AddYears(1)));
    }
}
