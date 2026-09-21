using MediatR;
using Microsoft.AspNetCore.Mvc;
using VisaTelegramBot.Application.Common.Results;

namespace VisaTelegramBot.WebApi.Controllers;

/// <summary>
/// Ince controller: is kurali icermez. Istegi command/query'ye cevirir, MediatR'a gonderir,
/// Result'i HTTP yanitina cevirir. Tum is mantigi Application katmanindadir.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiController(ISender sender) : ControllerBase
{
    protected ISender Sender { get; } = sender;

    protected IActionResult HandleFailure(Error error)
    {
        if (error is ValidationError validationError)
        {
            var errors = validationError.Errors
                .GroupBy(item => item.Code)
                .ToDictionary(group => group.Key, group => group.Select(item => item.Message).ToArray());

            return ValidationProblem(new ValidationProblemDetails(errors)
            {
                Title = validationError.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }

        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.ExternalDependency => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status500InternalServerError
        };

        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, statusCode, title: error.Message);
        problem.Extensions["code"] = error.Code;

        return new ObjectResult(problem) { StatusCode = statusCode };
    }
}
