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
