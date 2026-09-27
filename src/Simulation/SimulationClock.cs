namespace Simulation;

public enum SimulationSpeed
{
    OneX = 1,
    FourX = 4
}

public sealed class SimulationClock
{
    public const double TickIntervalSeconds = 0.1;
    public const int MaximumTicksPerAdvance = 8;
    private const double TickToleranceSeconds = TickIntervalSeconds * 1e-12;

    private double _accumulator;

    public double SimulationTime { get; private set; }
    public bool IsPaused { get; private set; }
    public SimulationSpeed Speed { get; private set; } = SimulationSpeed.OneX;

    public int Advance(double frameDeltaSeconds, Action<double>? tick = null)
    {
        if (double.IsNaN(frameDeltaSeconds) || double.IsInfinity(frameDeltaSeconds) || frameDeltaSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameDeltaSeconds));
        }

        if (IsPaused)
        {
            return 0;
        }

        _accumulator += frameDeltaSeconds * (int)Speed;
        var tickCount = 0;
        while (_accumulator + TickToleranceSeconds >= TickIntervalSeconds
               && tickCount < MaximumTicksPerAdvance)
        {
            _accumulator = Math.Max(0, _accumulator - TickIntervalSeconds);
            SimulationTime += TickIntervalSeconds;
            tickCount++;
            tick?.Invoke(TickIntervalSeconds);
        }

        if (tickCount == MaximumTicksPerAdvance
            && _accumulator + TickToleranceSeconds >= TickIntervalSeconds)
        {
            _accumulator = 0;
        }

        return tickCount;
    }

    public void SetPaused(bool paused) => IsPaused = paused;

    public void SetSpeed(SimulationSpeed speed) => Speed = speed;

    public void Reset()
    {
        _accumulator = 0;
        SimulationTime = 0;
        IsPaused = false;
        Speed = SimulationSpeed.OneX;
    }
}
