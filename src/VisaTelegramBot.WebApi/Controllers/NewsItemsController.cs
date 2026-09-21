using MediatR;
using Microsoft.AspNetCore.Mvc;
using VisaTelegramBot.Application.Common.Results;
using VisaTelegramBot.Application.NewsItems;
using VisaTelegramBot.Application.NewsItems.Queries;
using VisaTelegramBot.Application.NewsItems.Requeue;
using VisaTelegramBot.Domain.NewsItems;

namespace VisaTelegramBot.WebApi.Controllers;

[Route("api/news-items")]
public sealed class NewsItemsController(ISender sender) : ApiController(sender)
{
    /// <summary>Haberleri en yeniden eskiye listeler. Sonraki sayfa icin yanittaki nextCursor degerini gonderin.</summary>
    [HttpGet]
    [ProducesResponseType<CursorPage<NewsItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? newsSourceId,
        [FromQuery] DeliveryStatus? status,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken,
        [FromQuery] int pageSize = ListNewsItemsQuery.DefaultPageSize)
    {
        var result = await Sender.Send(new ListNewsItemsQuery(newsSourceId, status, cursor, pageSize), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result.Error);
    }

    /// <summary>Durumlara gore haber sayilari ve son gonderim zamani.</summary>
    [HttpGet("stats")]
    [ProducesResponseType<NewsItemStatsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Stats(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetNewsItemStatsQuery(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result.Error);
    }

    /// <summary>Basarisiz veya arsivlenmis bir haberi tekrar gonderim kuyruguna alir.</summary>
    [HttpPost("{id:guid}/requeue")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Requeue(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new RequeueNewsItemCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result.Error);
    }
}
