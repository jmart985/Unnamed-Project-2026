using System.Numerics;

namespace Simulation;

public sealed class SettlementBed
{
    public SettlementBed(string id, Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A bed ID is required.", nameof(id));
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z)) throw new ArgumentOutOfRangeException(nameof(position));
        Id = id; Position = position;
    }
    public string Id { get; }
    public Vector3 Position { get; }
    public string? OccupantId { get; private set; }
    public bool IsOccupied => OccupantId is not null;
    public bool TryReserve(string? entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId) || IsOccupied) return false;
        OccupantId = entityId;
        return true;
    }
    public bool Release(string? entityId)
    {
        if (!string.Equals(OccupantId, entityId, StringComparison.Ordinal)) return false;
        OccupantId = null;
        return true;
    }
}

public enum SleepState { Awake, Sleeping }

public sealed class SettlementBedRegistry
{
    private readonly List<SettlementBed> _beds = [];
    public IReadOnlyList<SettlementBed> Beds => _beds;
    public SettlementBed Add(SettlementBed bed)
    {
        ArgumentNullException.ThrowIfNull(bed);
        if (_beds.Any(existing => existing.Id == bed.Id)) throw new ArgumentException($"Bed ID '{bed.Id}' is already registered.", nameof(bed));
        _beds.Add(bed);
        return bed;
    }
    public bool TryAssignAvailable(string? entityId, out SettlementBed? bed)
    {
        bed = _beds.FirstOrDefault(candidate => !candidate.IsOccupied);
        return bed is not null && bed.TryReserve(entityId);
    }
    public void Reset()
    {
        foreach (var bed in _beds) bed.Release(bed.OccupantId);
    }
}

public sealed class CharacterSleep
{
    public SleepState State { get; private set; }
    public string? BedId { get; private set; }
    public bool TryBegin(SettlementBed? bed, string? entityId)
    {
        if (bed is null || string.IsNullOrWhiteSpace(entityId) || !string.Equals(bed.OccupantId, entityId, StringComparison.Ordinal)) return false;
        State = SleepState.Sleeping;
        BedId = bed.Id;
        return true;
    }
    public void Wake()
    {
        State = SleepState.Awake;
        BedId = null;
    }
}
