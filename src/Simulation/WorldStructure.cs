using System.Collections.Generic;
using System.Numerics;

namespace Simulation;

public enum StructureType
{
    Hut,
    Cave,
    Building,
    Fortification
}

public sealed class Bed
{
    public string Id { get; }
    public double RecoveryRateMultiplier { get; }
    public bool IsOccupied { get; internal set; }
    public string? OccupantId { get; internal set; }

    public Bed(string id, double recoveryRateMultiplier = 1.0)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Bed ID is required.");
        if (recoveryRateMultiplier <= 0) throw new ArgumentOutOfRangeException(nameof(recoveryRateMultiplier));
        
        Id = id;
        RecoveryRateMultiplier = recoveryRateMultiplier;
    }
}

public sealed class Structure
{
    public string Id { get; }
    public StructureType Type { get; }
    public Vector3 Position { get; }
    public bool IsClosed { get; set; }
    public List<Bed> Beds { get; } = new();

    public Structure(string id, StructureType type, Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Structure ID is required.");
        Id = id;
        Type = type;
        Position = position;
    }

    public void AddBed(Bed bed)
    {
        ArgumentNullException.ThrowIfNull(bed);
        Beds.Add(bed);
    }
}

public enum RestActionResult { Success, ActorUnavailable, ActorIncapacitated, NoShelter, OutOfRange, NoAvailableBed, AlreadyResting, NotResting, BedUnavailable }

public sealed record RestAction(RestActionResult Result, string Message, string? BedId = null)
{
    public bool Succeeded => Result == RestActionResult.Success;
}

public sealed class SettlementRestService
{
    private readonly World _world;
    public SettlementRestService(World world) => _world = world ?? throw new ArgumentNullException(nameof(world));

    public RestAction Sleep(string? actorId, string? structureId)
    {
        if (!_world.TryGetEntity(actorId, out var actor) || actor is null) return new(RestActionResult.ActorUnavailable, "The sleeper is unavailable.");
        if (actor.Health.IsIncapacitated) return new(RestActionResult.ActorIncapacitated, "The sleeper cannot rest while incapacitated.");
        if (actor.SleepState == SleepState.Sleeping) return new(RestActionResult.AlreadyResting, "Already resting.");
        var structure = _world.Structures.FirstOrDefault(item => item.Id == structureId && item.Type == StructureType.Hut && item.Beds.Count > 0);
        if (structure is null) return new(RestActionResult.NoShelter, "No completed shelter bed is available.");
        if (Vector3.Distance(actor.Position, structure.Position) > 2f) return new(RestActionResult.OutOfRange, "Move within 2 m of the shelter.");
        var bed = structure.Beds.FirstOrDefault(item => !item.IsOccupied);
        if (bed is null) return new(RestActionResult.NoAvailableBed, "All shelter beds are occupied.");
        bed.IsOccupied = true; bed.OccupantId = actor.Id;
        actor.SetSleepState(SleepState.Sleeping, bed.Id);
        actor.Activity = "Resting";
        return new(RestActionResult.Success, $"Resting in {bed.Id}.", bed.Id);
    }

    public RestAction Wake(string? actorId)
    {
        if (!_world.TryGetEntity(actorId, out var actor) || actor is null) return new(RestActionResult.ActorUnavailable, "The sleeper is unavailable.");
        if (actor.SleepState != SleepState.Sleeping || actor.BedId is null) return new(RestActionResult.NotResting, "The character is not resting.");
        foreach (var structure in _world.Structures)
        foreach (var bed in structure.Beds)
            if (bed.Id == actor.BedId && bed.OccupantId == actor.Id) { bed.IsOccupied = false; bed.OccupantId = null; }
        actor.SetSleepState(SleepState.Awake, null);
        actor.Activity = "Idle";
        return new(RestActionResult.Success, "Awake.");
    }
}
