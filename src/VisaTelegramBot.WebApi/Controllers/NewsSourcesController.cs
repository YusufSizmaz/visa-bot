using MediatR;
using Microsoft.AspNetCore.Mvc;
using VisaTelegramBot.Application.NewsSources;
using VisaTelegramBot.Application.NewsSources.ChangeStatus;
using VisaTelegramBot.Application.NewsSources.Create;
using VisaTelegramBot.Application.NewsSources.Fetch;
using VisaTelegramBot.Application.NewsSources.Queries;
using VisaTelegramBot.Application.NewsSources.Update;
using VisaTelegramBot.WebApi.Contracts;

namespace VisaTelegramBot.WebApi.Controllers;

[Route("api/news-sources")]
public sealed class NewsSourcesController(ISender sender) : ApiController(sender)
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NewsSourceResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ListNewsSourcesQuery(isActive), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result.Error);
    }

    [HttpGet("{id:guid}", Name = "GetNewsSourceById")]
    [ProducesResponseType<NewsSourceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetNewsSourceByIdQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result.Error);
    }

    [HttpPost]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateNewsSourceRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateNewsSourceCommand(
            request.Name,
            request.Url,
            request.Type,
            request.ParsingRules,
            request.FetchIntervalMinutes,
            request.Keywords,
            request.TurkishOnly,
            request.Category);

        var result = await Sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtRoute("GetNewsSourceById", new { id = result.Value }, new CreatedResponse(result.Value))
            : HandleFailure(result.Error);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNewsSourceRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateNewsSourceCommand(
            id,
            request.Name,
            request.Url,
            request.Type,
            request.ParsingRules,
            request.FetchIntervalMinutes,
            request.Keywords,
            request.TurkishOnly,
            request.Category);

        var result = await Sender.Send(command, cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result.Error);
    }

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ActivateNewsSourceCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result.Error);
    }

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeactivateNewsSourceCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result.Error);
    }

    /// <summary>Kaynagi zamanini beklemeden hemen ceker. Yeni eklenen kaynagi denemek icin kullanislidir.</summary>
    [HttpPost("{id:guid}/fetch")]
    [ProducesResponseType<FetchNewsSourceResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Fetch(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new FetchNewsSourceCommand(id, Force: true), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result.Error);
    }
}
