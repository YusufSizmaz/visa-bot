using FluentValidation;
using VisaTelegramBot.Application.Common.Behaviors;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Application.NewsItems;

namespace VisaTelegramBot.Application.Tests.Common;

public sealed class ResultAndPipelineTests
{
    [Fact]
    public void FailedResult_ValueAccess_Throws()
    {
        Result<int> result = Error.NotFound("X.NotFound", "yok");

        Assert.True(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public async Task ValidationBehavior_WithErrors_ShortCircuitsWithTypedFailure()
    {
        var behavior = new ValidationBehavior<SampleRequest, Result<string>>([new SampleValidator()]);
        var handlerCalled = false;

        var result = await behavior.Handle(
            new SampleRequest(string.Empty),
            _ =>
            {
                handlerCalled = true;
                return Task.FromResult(Result.Success("ok"));
            },
            CancellationToken.None);

        Assert.False(handlerCalled);
        Assert.True(result.IsFailure);
        var validationError = Assert.IsType<ValidationError>(result.Error);
        Assert.Single(validationError.Errors);
    }

    [Fact]
    public async Task ValidationBehavior_WithoutErrors_CallsHandler()
    {
        var behavior = new ValidationBehavior<SampleRequest, Result<string>>([new SampleValidator()]);

        var result = await behavior.Handle(
            new SampleRequest("dolu"),
            _ => Task.FromResult(Result.Success("ok")),
            CancellationToken.None);

        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public void NewsItemCursor_RoundTrips()
    {
        var cursor = new NewsItemCursor(new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc).AddTicks(7), Guid.CreateVersion7());

        Assert.True(NewsItemCursor.TryDecode(cursor.Encode(), out var decoded));
        Assert.Equal(cursor, decoded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("kısa")]
    [InlineData("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")]
    public void NewsItemCursor_RejectsGarbage(string value)
    {
        Assert.False(NewsItemCursor.TryDecode(value, out _));
    }

    public sealed record SampleRequest(string Name);

    private sealed class SampleValidator : AbstractValidator<SampleRequest>
    {
        public SampleValidator() => RuleFor(x => x.Name).NotEmpty();
    }
}
