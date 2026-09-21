using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsItems;
using VisaTelegramBot.Domain.NewsItems.Events;

namespace VisaTelegramBot.Domain.Tests.NewsItems;

public sealed class NewsItemTests
{
    [Fact]
    public void Discover_CreatesPendingItemAndRaisesEvent()
    {
        var item = TestData.PendingItem();

        Assert.Equal(DeliveryStatus.Pending, item.DeliveryStatus);
        Assert.True(item.IsDeliverable(TestData.Now));
        Assert.Single(item.DomainEvents.OfType<NewsItemDiscoveredDomainEvent>());
    }

    [Fact]
    public void Archive_CreatesArchivedItemWithoutEvent()
    {
        var item = NewsItem.Archive(
            Guid.CreateVersion7(), "Eski haber", WebUrl.Create("https://example.com/old"), null, null, TestData.Now);

        Assert.Equal(DeliveryStatus.Archived, item.DeliveryStatus);
        Assert.False(item.IsDeliverable(TestData.Now));
        Assert.Empty(item.DomainEvents);
    }

    [Fact]
    public void Discover_NormalizesWhitespaceInTitle()
    {
        var item = NewsItem.Discover(
            Guid.CreateVersion7(), "  Yeni\n\t  kural   ", WebUrl.Create("https://example.com/a"), "   ", null, TestData.Now);

        Assert.Equal("Yeni kural", item.Title);
        Assert.Null(item.Summary);
    }

    [Fact]
    public void Discover_WithTooLongTitle_Throws()
    {
        var title = new string('a', NewsItem.TitleMaxLength + 1);

        Assert.Throws<DomainException>(() => NewsItem.Discover(
            Guid.CreateVersion7(), title, WebUrl.Create("https://example.com/a"), null, null, TestData.Now));
    }

    [Fact]
    public void MarkAsDelivered_IsIdempotent()
    {
        var item = TestData.PendingItem();

        item.MarkAsDelivered("42", TestData.Now);
        item.MarkAsDelivered("43", TestData.Now.AddMinutes(1));

        Assert.Equal(DeliveryStatus.Delivered, item.DeliveryStatus);
        Assert.Equal("42", item.ExternalMessageId);
        Assert.Equal(1, item.DeliveryAttempts);
    }

    [Fact]
    public void RecordDeliveryFailure_Transient_SchedulesRetryWithBackoff()
    {
        var item = TestData.PendingItem();

        item.RecordDeliveryFailure("ağ hatası", isPermanent: false, TestData.Now);

        Assert.Equal(DeliveryStatus.Pending, item.DeliveryStatus);
        Assert.Equal(1, item.DeliveryAttempts);
        Assert.False(item.IsDeliverable(TestData.Now));
        Assert.True(item.IsDeliverable(TestData.Now + NewsItem.BaseRetryDelay));
    }

    [Fact]
    public void RecordDeliveryFailure_AfterMaxAttempts_FailsAndRaisesEvent()
    {
        var item = TestData.PendingItem();

        for (var i = 0; i < NewsItem.MaxDeliveryAttempts; i++)
        {
            item.RecordDeliveryFailure("ağ hatası", isPermanent: false, TestData.Now);
        }

        Assert.Equal(DeliveryStatus.Failed, item.DeliveryStatus);
        Assert.Single(item.DomainEvents.OfType<NewsItemDeliveryFailedDomainEvent>());
    }

    [Fact]
    public void RecordDeliveryFailure_Permanent_FailsImmediately()
    {
        var item = TestData.PendingItem();

        item.RecordDeliveryFailure("geçersiz mesaj", isPermanent: true, TestData.Now);

        Assert.Equal(DeliveryStatus.Failed, item.DeliveryStatus);
    }

    [Fact]
    public void DeferDelivery_DoesNotConsumeAttempt()
    {
        var item = TestData.PendingItem();

        item.DeferDelivery(TimeSpan.FromSeconds(30), TestData.Now);

        Assert.Equal(0, item.DeliveryAttempts);
        Assert.False(item.IsDeliverable(TestData.Now.AddSeconds(29)));
        Assert.True(item.IsDeliverable(TestData.Now.AddSeconds(30)));
    }

    [Fact]
    public void RequeueForDelivery_FromFailed_ResetsAttempts()
    {
        var item = TestData.PendingItem();
        item.RecordDeliveryFailure("hata", isPermanent: true, TestData.Now);

        item.RequeueForDelivery(TestData.Now.AddHours(1));

        Assert.Equal(DeliveryStatus.Pending, item.DeliveryStatus);
        Assert.Equal(0, item.DeliveryAttempts);
    }

    [Fact]
    public void RequeueForDelivery_WhenDelivered_Throws()
    {
        var item = TestData.PendingItem();
        item.MarkAsDelivered("1", TestData.Now);

        Assert.Throws<DomainException>(() => item.RequeueForDelivery(TestData.Now));
    }

    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(4, 240)]
    [InlineData(20, 3600)]
    public void CalculateRetryDelay_GrowsExponentiallyAndIsCapped(int attempt, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), NewsItem.CalculateRetryDelay(attempt));
    }
}
