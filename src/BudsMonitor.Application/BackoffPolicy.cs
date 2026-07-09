namespace BudsMonitor.Application;

/// <summary>
/// Computes an exponential backoff delay from a consecutive-failure count, capped at a
/// maximum. Used to slow repeated provider polling and scanner restarts after failures,
/// and to recover promptly once an attempt succeeds (callers reset the count to zero).
/// </summary>
public sealed class BackoffPolicy
{
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;

    public BackoffPolicy(TimeSpan baseDelay, TimeSpan maxDelay)
    {
        if (baseDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(baseDelay), "Base delay must be positive.");
        }

        if (maxDelay < baseDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDelay), "Max delay must be >= base delay.");
        }

        _baseDelay = baseDelay;
        _maxDelay = maxDelay;
    }

    /// <summary>
    /// Delay after <paramref name="failureCount"/> consecutive failures. A count of 0 (or less)
    /// means no failures and returns <see cref="TimeSpan.Zero"/>. Otherwise the delay is
    /// base × 2^(count-1), capped at the configured maximum (1→base, 2→2×base, 3→4×base, …).
    /// </summary>
    public TimeSpan DelayFor(int failureCount)
    {
        if (failureCount <= 0)
        {
            return TimeSpan.Zero;
        }

        // Cap the exponent so the multiplication cannot overflow before the max clamp.
        var factor = Math.Pow(2, Math.Min(failureCount - 1, 30));
        var ticks = _baseDelay.Ticks * factor;

        return double.IsInfinity(ticks) || ticks >= _maxDelay.Ticks
            ? _maxDelay
            : TimeSpan.FromTicks((long)ticks);
    }
}
