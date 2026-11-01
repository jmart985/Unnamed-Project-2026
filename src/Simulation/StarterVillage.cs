using System.Numerics;

namespace Simulation;

public static class StarterVillage
{
    public const float Radius = 44;
    public const double PlaytestDaySeconds = 120;

    public static (Vector3 Position, int FacingQuarterTurns) Layout(int index, int count)
    {
        if (count <= 0 || index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
        var angle = Math.PI / 20 + 2 * Math.PI * index / count;
        var position = new Vector3((float)Math.Round(Radius * Math.Cos(angle)), 0,
            (float)Math.Round(Radius * Math.Sin(angle)));
        var facing = Math.Abs(position.X) > Math.Abs(position.Z)
            ? position.X > 0 ? 3 : 1 : position.Z > 0 ? 2 : 0;
        return (position, facing);
    }

    internal static void CreateHomes(World world, IReadOnlyList<EntityRecord> residents)
    {
        world.UsesVillageLayout = true;
        for (var index = 0; index < residents.Count; index++)
        {
            var resident = residents[index];
            var (position, facing) = Layout(index, residents.Count);
            var home = world.AddStructure($"home-{resident.Id}", StructureType.Hut, position, facing, resident.Id);
            home.AddBed(new Bed($"{home.Id}-bed"));
            world.SetHomePosition(resident.Id, home.Position);
        }
    }
}

public sealed partial class World
{
    public bool UsesVillageLayout { get; internal set; }
    public double DayLengthSeconds { get; private set; } = DailyScheduleState.SecondsPerDay;

    public void SetDayLength(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 24 || seconds > 86400)
            throw new ArgumentOutOfRangeException(nameof(seconds), "Day length must be between 24 and 86400 seconds.");
        DayLengthSeconds = seconds;
    }

    public DailyScheduleState EvaluateWorkSchedule(string entityId, double simulationTime)
    {
        var scale = DailyScheduleState.SecondsPerDay / DayLengthSeconds;
        var phase = GetWorkSchedule(entityId).Evaluate(simulationTime * scale);
        return phase with { SecondsUntilTransition = phase.SecondsUntilTransition / scale };
    }

    public double DayHour(double simulationTime) => DailyScheduleState.DaySeconds(
        simulationTime * DailyScheduleState.SecondsPerDay / DayLengthSeconds) / DailyScheduleState.SecondsPerHour;
}
