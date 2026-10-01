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
    string? WaitingReason,
    IReadOnlyList<RelationshipInspectionSnapshot> OutgoingRelationships);

public sealed record RelationshipInspectionSnapshot(
    string TargetEntityId,
    double Familiarity,
    double Trust,
    double Fear,
    double Loyalty,
    RelationshipBondLevel BondLevel);

/// <summary>Creates immutable, read-only inspection data from authoritative entity state.</summary>
public static class EntityInspectionReadModel
{
    public static EntityInspectionSnapshot Create(World world, EntityRecord entity)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entity);
        var inventory = new ReadOnlyDictionary<string, int>(entity.Inventory.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        var relationships = world.Relationships
            .Where(relationship => string.Equals(
                relationship.ObserverEntityId,
                entity.Id,
                StringComparison.Ordinal)
                && world.TryGetEntity(relationship.TargetEntityId, out _))
            .OrderBy(relationship => relationship.TargetEntityId, StringComparer.Ordinal)
            .Select(relationship => new RelationshipInspectionSnapshot(
                relationship.TargetEntityId,
                relationship.Familiarity,
                relationship.Trust,
                relationship.Fear,
                relationship.Loyalty,
                RelationshipBondClassifier.Classify(relationship)))
            .ToArray();
        return new EntityInspectionSnapshot(
            entity.Id,
            entity.SpeciesId,
            entity.Position,
            entity.Satiety,
            entity.Activity,
            entity.Actions.Current?.Id,
            inventory,
            entity.WaitingReason,
            Array.AsReadOnly(relationships));
    }

    public static string FormatOutgoingRelationships(EntityInspectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.OutgoingRelationships.Count == 0)
        {
            return "none";
        }

        return string.Join(
            "\n",
            snapshot.OutgoingRelationships.Select(relationship =>
                FormattableString.Invariant(
                    $"{relationship.TargetEntityId}: Familiarity {relationship.Familiarity:0.0}, Trust {relationship.Trust:0.0}, Fear {relationship.Fear:0.0}, Loyalty {relationship.Loyalty:0.0} [{relationship.BondLevel}]")));
    }
}
