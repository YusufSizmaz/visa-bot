using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.Abstractions.Publishing;
using VisaTelegramBot.Application.NewsItems.Delivery;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsItems;

namespace VisaTelegramBot.Application.Tests.NewsItems;

public sealed class DeliverNewsItemCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly INewsItemRepository _items = Substitute.For<INewsItemRepository>();
    private readonly INewsSourceRepository _sources = Substitute.For<INewsSourceRepository>();
    private readonly INewsPublisher _publisher = Substitute.For<INewsPublisher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly NewsItem _item;

    public DeliverNewsItemCommandHandlerTests()
    {
        _item = NewsItem.Discover(
            Guid.CreateVersion7(), "Başlık", WebUrl.Create("https://example.com/1"), null, null, Now.UtcDateTime);

        _items.GetByIdAsync(_item.Id, Arg.Any<CancellationToken>()).Returns(_item);
    }

    [Fact]
    public async Task Published_MarksItemAsDelivered()
    {
        GivenPublishResult(PublishResult.Published("777"));

        var result = await Handle();

        Assert.Equal(DeliveryOutcome.Delivered, result.Outcome);
        Assert.Equal(DeliveryStatus.Delivered, _item.DeliveryStatus);
        Assert.Equal("777", _item.ExternalMessageId);
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RateLimited_DefersWithoutConsumingAttempt()
    {
        GivenPublishResult(PublishResult.RateLimited(TimeSpan.FromSeconds(12)));

        var result = await Handle();

        Assert.Equal(DeliveryOutcome.Deferred, result.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(12), result.RetryAfter);
        Assert.Equal(0, _item.DeliveryAttempts);
        Assert.Equal(DeliveryStatus.Pending, _item.DeliveryStatus);
    }

    [Fact]
    public async Task TransientFailure_SchedulesRetry()
    {
        GivenPublishResult(PublishResult.TransientFailure("timeout"));

        var result = await Handle();

        Assert.Equal(DeliveryOutcome.RetryScheduled, result.Outcome);
        Assert.Equal(1, _item.DeliveryAttempts);
    }

    [Fact]
    public async Task PermanentFailure_FailsItem()
    {
        GivenPublishResult(PublishResult.PermanentFailure("bad request"));

        var result = await Handle();

        Assert.Equal(DeliveryOutcome.Failed, result.Outcome);
        Assert.Equal(DeliveryStatus.Failed, _item.DeliveryStatus);
    }

    [Fact]
    public async Task AlreadyDeliveredItem_IsNotPublishedAgain()
    {
        _item.MarkAsDelivered("1", Now.UtcDateTime);

        var result = await Handle();

        Assert.Equal(DeliveryOutcome.AlreadyProcessed, result.Outcome);
        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default);
    }

    private void GivenPublishResult(PublishResult result)
    {
        _publisher.PublishAsync(Arg.Any<NewsMessage>(), Arg.Any<CancellationToken>()).Returns(result);
    }

    private async Task<DeliverNewsItemResult> Handle()
    {
        var handler = new DeliverNewsItemCommandHandler(_items, _sources, _publisher, _unitOfWork, new FakeTimeProvider(Now));
        var result = await handler.Handle(new DeliverNewsItemCommand(_item.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
