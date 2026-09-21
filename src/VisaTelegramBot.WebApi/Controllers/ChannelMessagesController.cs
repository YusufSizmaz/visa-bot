using MediatR;
using Microsoft.AspNetCore.Mvc;
using VisaTelegramBot.Application.ChannelMessages;
using VisaTelegramBot.Domain.ChannelMessages;
using VisaTelegramBot.WebApi.Contracts;

namespace VisaTelegramBot.WebApi.Controllers;

[Route("api/channel-messages")]
public sealed class ChannelMessagesController(ISender sender) : ApiController(sender)
{
    private const long MaxUploadBytes = MessagePhoto.MaxSizeBytes + (512 * 1024);

    [HttpGet]
    [ProducesResponseType<ChannelMessagePage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] ChannelMessageStatus? status,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Sender.Send(new ListChannelMessagesQuery(status, page, pageSize), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result.Error);
    }

    /// <summary>Yeni mesaj. Gorsel yuklenebildigi icin JSON yerine multipart/form-data kabul eder.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromForm] CreateChannelMessageRequest request, CancellationToken cancellationToken)
    {
        byte[]? photo = null;

        if (request.Photo is { Length: > 0 })
        {
            await using var stream = new MemoryStream();
            await request.Photo.CopyToAsync(stream, cancellationToken);
            photo = stream.ToArray();
        }

        var command = new CreateChannelMessageCommand(
            request.Title,
            request.Body,
            request.LinkUrl,
            request.ButtonText,
            request.ButtonUrl,
            photo,
            request.Photo?.FileName,
            request.ScheduledAt?.UtcDateTime);

        var result = await Sender.Send(command, cancellationToken);

        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, new CreatedResponse(result.Value)) : HandleFailure(result.Error);
    }

    [HttpGet("{id:guid}/photo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Photo(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetChannelMessagePhotoQuery(id), cancellationToken);

        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType) : HandleFailure(result.Error);
    }

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) =>
        Change(id, ChannelMessageAction.Cancel, cancellationToken);

    [HttpPost("{id:guid}/send-now")]
    public Task<IActionResult> SendNow(Guid id, CancellationToken cancellationToken) =>
        Change(id, ChannelMessageAction.SendNow, cancellationToken);

    [HttpPost("{id:guid}/retry")]
    public Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken) =>
        Change(id, ChannelMessageAction.Retry, cancellationToken);

    private async Task<IActionResult> Change(Guid id, ChannelMessageAction action, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ChangeChannelMessageCommand(id, action), cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result.Error);
    }
}

[Route("api/channel")]
public sealed class ChannelController(ISender sender) : ApiController(sender)
{
    /// <summary>Kanal adi, abone sayisi ve bekleyen mesaj sayisi.</summary>
    [HttpGet]
    [ProducesResponseType<ChannelOverviewResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetChannelOverviewQuery(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result.Error);
    }
}
