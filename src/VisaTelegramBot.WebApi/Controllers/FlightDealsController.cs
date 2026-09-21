using MediatR;
using Microsoft.AspNetCore.Mvc;
using VisaTelegramBot.Application.FlightDeals;
using VisaTelegramBot.Domain.FlightDeals;
using VisaTelegramBot.WebApi.Contracts;

namespace VisaTelegramBot.WebApi.Controllers;

[Route("api/flight-routes")]
public sealed class FlightRoutesController(ISender sender) : ApiController(sender)
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<FlightRouteResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ListFlightRoutesQuery(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result.Error);
    }

    [HttpPost]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    public Task<IActionResult> Create([FromBody] SaveFlightRouteRequest request, CancellationToken cancellationToken) =>
        Save(null, request, cancellationToken);

    [HttpPut("{id:guid}")]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> Update(Guid id, [FromBody] SaveFlightRouteRequest request, CancellationToken cancellationToken) =>
        Save(id, request, cancellationToken);

    [HttpPost("{id:guid}/activate")]
    public Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken) => SetActive(id, true, cancellationToken);

    [HttpPost("{id:guid}/deactivate")]
    public Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) => SetActive(id, false, cancellationToken);

    /// <summary>Rotayi zamanini beklemeden hemen kontrol eder.</summary>
    [HttpPost("{id:guid}/check")]
    [ProducesResponseType<CheckFlightRouteResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Check(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CheckFlightRouteCommand(id, Force: true), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result.Error);
    }

    private async Task<IActionResult> Save(Guid? id, SaveFlightRouteRequest request, CancellationToken cancellationToken)
    {
        var command = new SaveFlightRouteCommand(
            id, request.Origin, request.Destination, request.Label, request.MaxPrice,
            request.MonthsAhead, request.CheckIntervalMinutes, request.AutoPublish);

        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result.Error);
        }

        return id is null ? StatusCode(StatusCodes.Status201Created, new CreatedResponse(result.Value)) : Ok(new CreatedResponse(result.Value));
    }

    private async Task<IActionResult> SetActive(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new SetFlightRouteActiveCommand(id, isActive), cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result.Error);
    }
}

[Route("api/flight-deals")]
public sealed class FlightDealsController(ISender sender) : ApiController(sender)
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<FlightDealResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] FlightDealStatus? status, CancellationToken cancellationToken, [FromQuery] int limit = 50)
    {
        var result = await Sender.Send(new ListFlightDealsQuery(status, limit), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result.Error);
    }

    /// <summary>Firsati Turkce mesaja donusturup kanala gonderim sirasina alir.</summary>
    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new PublishFlightDealCommand(id), cancellationToken);

        return result.IsSuccess ? Ok(new CreatedResponse(result.Value)) : HandleFailure(result.Error);
    }

    [HttpPost("{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DismissFlightDealCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result.Error);
    }
}
