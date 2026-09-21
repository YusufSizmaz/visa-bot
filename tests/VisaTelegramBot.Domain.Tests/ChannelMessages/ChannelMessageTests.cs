using VisaTelegramBot.Domain.ChannelMessages;
using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Domain.Tests.ChannelMessages;

public sealed class ChannelMessageTests
{
    private static readonly DateTime Now = TestData.Now;

    // 1x1 piksel PNG'nin imzasi yeterli; icerigin tamamina bakilmaz.
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];

    [Fact]
    public void Create_WithoutSchedule_IsDueImmediately()
    {
        var message = Create();

        Assert.Equal(ChannelMessageStatus.Scheduled, message.Status);
        Assert.True(message.IsDue(Now));
    }

    [Fact]
    public void Create_WithFutureSchedule_IsNotDueUntilThen()
    {
        var message = Create(scheduledAtUtc: Now.AddHours(2));

        Assert.False(message.IsDue(Now.AddHours(1)));
        Assert.True(message.IsDue(Now.AddHours(2)));
    }

    [Fact]
    public void Create_WithPastSchedule_SendsNow()
    {
        var message = Create(scheduledAtUtc: Now.AddDays(-1));

        Assert.Equal(Now, message.ScheduledAtUtc);
    }

    [Fact]
    public void Create_WithEmptyBody_Throws()
    {
        Assert.Throws<DomainException>(() => ChannelMessage.Create(ChannelMessageKind.Custom, "Başlık", "   ", null, null, null, null, Now));
    }

    [Fact]
    public void Create_WithPhotoAndLongText_ThrowsBecauseOfTelegramCaptionLimit()
    {
        var photo = MessagePhoto.Create(PngBytes, "a.png");
        var body = new string('a', ChannelMessage.PhotoCaptionMaxLength + 1);

        Assert.Throws<DomainException>(() => ChannelMessage.Create(ChannelMessageKind.Custom, null, body, null, null, photo, null, Now));
    }

    [Fact]
    public void Cancel_ThenSendNow_Throws()
    {
        var message = Create(scheduledAtUtc: Now.AddHours(1));
        message.Cancel();

        Assert.Equal(ChannelMessageStatus.Cancelled, message.Status);
        Assert.Throws<DomainException>(() => message.SendNow(Now));
    }

    [Fact]
    public void MarkAsSent_IsIdempotent_AndCannotBeCancelledAfter()
    {
        var message = Create();

        message.MarkAsSent("10", Now);
        message.MarkAsSent("11", Now);

        Assert.Equal("10", message.ExternalMessageId);
        Assert.Throws<DomainException>(message.Cancel);
    }

    [Fact]
    public void RecordFailure_RetriesWithBackoff_ThenFails_AndCanBeRetried()
    {
        var message = Create();

        message.RecordFailure("timeout", isPermanent: false, Now);
        Assert.Equal(Now + RetryPolicy.DelayFor(1), message.ScheduledAtUtc);

        for (var i = 1; i < RetryPolicy.MaxAttempts; i++)
        {
            message.RecordFailure("timeout", isPermanent: false, Now);
        }

        Assert.Equal(ChannelMessageStatus.Failed, message.Status);

        message.Retry(Now.AddHours(1));

        Assert.Equal(ChannelMessageStatus.Scheduled, message.Status);
        Assert.Equal(0, message.Attempts);
    }

    [Fact]
    public void Photo_RejectsNonImageContent()
    {
        Assert.Throws<DomainException>(() => MessagePhoto.Create("GIF89a-bu-bir-gif"u8.ToArray(), "a.gif"));
    }

    [Fact]
    public void Photo_DetectsTypeFromContent_NotFileName()
    {
        var photo = MessagePhoto.Create(PngBytes, "../../gizli/resim.jpg");

        Assert.Equal("image/png", photo.ContentType);
        Assert.Equal("resim.jpg", photo.FileName);
    }

    [Fact]
    public void Button_RequiresText()
    {
        Assert.Throws<DomainException>(() => MessageButton.Create(" ", WebUrl.Create("https://example.com")));
    }

    private static ChannelMessage Create(DateTime? scheduledAtUtc = null) =>
        ChannelMessage.Create(ChannelMessageKind.Custom, "Duyuru", "İspanya randevuları açıldı.", null, null, null, scheduledAtUtc, Now);
}
