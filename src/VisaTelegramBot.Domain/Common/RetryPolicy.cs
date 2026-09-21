namespace VisaTelegramBot.Domain.Common;

/// <summary>
/// Teslimati olan tum aggregate'lerin (haber, kanal mesaji) ortak yeniden deneme kurali.
/// Exponential backoff: 30sn, 1dk, 2dk, 4dk... en fazla 1 saat.
/// </summary>
public static class RetryPolicy
{
    public const int MaxAttempts = 5;
    public static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromHours(1);

    public static TimeSpan DelayFor(int attempt)
    {
        if (attempt <= 1)
        {
            return BaseDelay;
        }

        var exponent = Math.Min(attempt - 1, 16);
        var delay = TimeSpan.FromTicks(BaseDelay.Ticks * (1L << exponent));

        return delay > MaxDelay ? MaxDelay : delay;
    }
}
