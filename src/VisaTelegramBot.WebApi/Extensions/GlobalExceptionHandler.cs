using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.WebApi.Extensions;

/// <summary>
/// Yakalanmamis tum hatalari tek noktada standart ProblemDetails (RFC 9457) bicimine cevirir.
/// Istemciye asla stack trace sizdirilmaz.
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "Kayıt eş zamanlı olarak değiştirildi. Güncel veriyi alıp tekrar deneyin."),
            UniqueConstraintViolationException => (StatusCodes.Status409Conflict, "Bu kayıt zaten mevcut."),
            DomainException => (StatusCodes.Status400BadRequest, exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen bir hata oluştu.")
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "İşlenmeyen hata: {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("İstek reddedildi ({StatusCode}): {Message}", statusCode, exception.Message);
        }

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title
            }
        });
    }
}
