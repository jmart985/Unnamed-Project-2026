using System.Collections.ObjectModel;
using System.Numerics;

namespace Simulation;

public sealed record EntityInspectionSnapshot(
    string Id,
    string SpeciesId,
    Vector3 Position,
    double Satiety,
    string Activity,
    string? Target,
    IReadOnlyDictionary<string, int> Inventory,
    string? WaitingReason);

/// <summary>Creates immutable, read-only inspection data from authoritative entity state.</summary>
public static class EntityInspectionReadModel
{
    public static EntityInspectionSnapshot Create(EntityRecord entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var inventory = new ReadOnlyDictionary<string, int>(entity.Inventory.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        return new EntityInspectionSnapshot(
            entity.Id,
            entity.SpeciesId,
            entity.Position,
            entity.Satiety,
            entity.Activity,
            entity.Actions.Current?.Id,
            inventory,
            entity.WaitingReason);
    }
}
