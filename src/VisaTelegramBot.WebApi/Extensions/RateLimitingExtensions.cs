using System.Threading.RateLimiting;

namespace VisaTelegramBot.WebApi.Extensions;

internal static class RateLimitingExtensions
{
    public const string ApiPolicy = "api";

    /// <summary>
    /// Istemci basina sabit pencere limiti. Hatali bir script veya kotu niyetli istemci API'yi ve veritabanini bogamaz.
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(ApiPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });

        return services;
    }
}
