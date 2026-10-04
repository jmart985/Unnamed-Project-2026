namespace Simulation;

public enum ScheduleUpdateResult { Accepted, Unchanged, InvalidEntity, IneligibleEntity, InvalidHours }

public sealed record NpcWorkSchedule(int StartHour, int EndHour)
{
    public static NpcWorkSchedule Default { get; } = new(8, 18);
    public bool IsValid => StartHour is >= 0 and < 24 && EndHour is >= 0 and < 24 && StartHour != EndHour;

    public DailyScheduleState Evaluate(double simulationTime)
    {
        if (!IsValid) throw new InvalidOperationException("Work hours must be distinct hours within [0, 24).");
        var seconds = DailyScheduleState.DaySeconds(simulationTime);
        var start = StartHour * DailyScheduleState.SecondsPerHour;
        var end = EndHour * DailyScheduleState.SecondsPerHour;
        var working = start < end ? seconds >= start && seconds < end : seconds >= start || seconds < end;
        var next = working ? end : start;
        var until = (next - seconds + DailyScheduleState.SecondsPerDay) % DailyScheduleState.SecondsPerDay;
        return new(working, seconds / DailyScheduleState.SecondsPerHour, next / DailyScheduleState.SecondsPerHour, until);
    }
}

public sealed record NpcScheduleSetting(string EntityId, int StartHour, int EndHour);
public sealed record NpcHomeSetting(string EntityId, System.Numerics.Vector3 Position);

public sealed record DailyScheduleState(bool IsWorking, double Hour, double NextTransitionHour, double SecondsUntilTransition)
{
    public const double SecondsPerHour = 10;
    public const double SecondsPerDay = 24 * SecondsPerHour;

    public static double DaySeconds(double simulationTime)
    {
        if (!double.IsFinite(simulationTime) || simulationTime < 0)
            throw new ArgumentOutOfRangeException(nameof(simulationTime));
        // The fixed-step clock can accumulate floating-point error at exact hour boundaries.
        return Math.Round((simulationTime % SecondsPerDay + 8 * SecondsPerHour) % SecondsPerDay, 8) % SecondsPerDay;
    }
}
