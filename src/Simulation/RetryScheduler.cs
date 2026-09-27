namespace Simulation;

/// <summary>
/// Shared bounded-retry policy for domain jobs that must wait for a condition (available
/// resource, reachable target, free capacity, etc.) without reselecting or reevaluating
/// every simulation tick. Callers key retries per waiting subject (typically an entity ID)
/// and schedule a single retry tick after each failed attempt.
/// </summary>
public sealed class RetryScheduler
{
    /// <summary>
    /// Default delay of 10 simulation ticks (1.0 simulated second at the 0.1-second tick
    /// rate), matching the bounded food-selection retry documented in DECISIONS.md D011.
    /// </summary>
    public const int DefaultDelayTicks = 10;

    private readonly int _delayTicks;
    private readonly Dictionary<string, long> _retryAtTicks = new(StringComparer.Ordinal);

    public RetryScheduler(int delayTicks = DefaultDelayTicks)
    {
        if (delayTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delayTicks), "Retry delay must be positive.");
        }

        _delayTicks = delayTicks;
    }

    /// <summary>
    /// Returns true when <paramref name="key"/> is still within its scheduled retry delay
    /// at <paramref name="simulationTick"/>, meaning the caller should keep waiting instead
    /// of reevaluating its candidates.
    /// </summary>
    public bool IsWaiting(string key, long simulationTick)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A retry key is required.", nameof(key));
        }

        if (simulationTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(simulationTick), "Simulation tick cannot be negative.");
        }

        if (!_retryAtTicks.TryGetValue(key, out var retryAtTick))
        {
            return false;
        }

        if (simulationTick < retryAtTick)
        {
            return true;
        }

        _retryAtTicks.Remove(key);
        return false;
    }

    /// <summary>
    /// Schedules the next retry for <paramref name="key"/> at least <see cref="DefaultDelayTicks"/>
    /// (or the configured delay) simulation ticks after <paramref name="simulationTick"/>.
    /// </summary>
    public void ScheduleRetry(string key, long simulationTick)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A retry key is required.", nameof(key));
        }

        if (simulationTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(simulationTick), "Simulation tick cannot be negative.");
        }

        _retryAtTicks[key] = simulationTick > long.MaxValue - _delayTicks
            ? long.MaxValue
            : simulationTick + _delayTicks;
    }

    /// <summary>Clears any pending retry for <paramref name="key"/>, e.g. once it succeeds.</summary>
    public void Clear(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        _retryAtTicks.Remove(key);
    }

    /// <summary>Clears all scheduled retries.</summary>
    public void Reset()
    {
        _retryAtTicks.Clear();
    }
}
