using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using VisaTelegramBot.Application.Common.Results;

namespace VisaTelegramBot.Application.Common.Behaviors;

/// <summary>
/// Pipeline Behavior: her command/query'nin etrafina sarilan kesisen ilgi (cross-cutting concern).
/// Decorator ve Chain of Responsibility desenlerinin birlesimidir. Loglamayi her handler'da tekrar yazmayiz.
/// </summary>
internal sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var startedAt = Stopwatch.GetTimestamp();

        var response = await next(cancellationToken);

        var elapsed = Stopwatch.GetElapsedTime(startedAt);

        if (response.IsFailure)
        {
            logger.LogWarning(
                "{RequestName} başarısız oldu ({ElapsedMs} ms). Hata: {ErrorCode} {ErrorMessage}",
                requestName,
                (long)elapsed.TotalMilliseconds,
                response.Error.Code,
                response.Error.Message);
        }
        else
        {
            logger.LogDebug(
                "{RequestName} tamamlandı ({ElapsedMs} ms).",
                requestName,
                (long)elapsed.TotalMilliseconds);
        }

        return response;
    }
}
