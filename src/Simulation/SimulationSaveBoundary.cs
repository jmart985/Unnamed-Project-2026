namespace Simulation;

public enum SaveBoundaryStatus
{
    Accepted,
    RequiresPausedBoundary,
    InvalidSnapshot
}

public sealed record SaveBoundaryResult(
    SaveBoundaryStatus Status,
    string? Snapshot,
    double SimulationTime,
    string? ErrorCode);

/// <summary>Coordinates versioned snapshots with the simulation clock at safe tick boundaries.</summary>
public sealed class SimulationSaveBoundary
{
    public SaveBoundaryResult Save(World world, SimulationClock clock)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(clock);
        if (!clock.IsPaused)
        {
            return new(SaveBoundaryStatus.RequiresPausedBoundary, null, clock.SimulationTime, "save_requires_paused_boundary");
        }

        return new(SaveBoundaryStatus.Accepted, WorldSnapshotSerializer.Serialize(world, clock.SimulationTime), clock.SimulationTime, null);
    }

    public SaveBoundaryResult Load(
        World world,
        SimulationClock clock,
        string json,
        SpeciesCatalog speciesCatalog,
        params ITransientSimulationState[] transientState)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(speciesCatalog);
        if (!clock.IsPaused)
        {
            return new(SaveBoundaryStatus.RequiresPausedBoundary, null, clock.SimulationTime, "load_requires_paused_boundary");
        }

        try
        {
            var time = WorldSnapshotSerializer.Load(world, json, speciesCatalog, transientState);
            clock.Restore(time);
            return new(SaveBoundaryStatus.Accepted, json, time, null);
        }
        catch (ArgumentException)
        {
            return new(SaveBoundaryStatus.InvalidSnapshot, null, clock.SimulationTime, "invalid_snapshot");
        }
        catch (InvalidDataException)
        {
            return new(SaveBoundaryStatus.InvalidSnapshot, null, clock.SimulationTime, "invalid_snapshot");
        }
    }
}
