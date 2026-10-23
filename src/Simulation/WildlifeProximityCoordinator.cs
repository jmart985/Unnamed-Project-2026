using System.Numerics;

namespace Simulation;

public sealed record WildlifeProximityUpdate(IReadOnlyList<string> ActivatedEntityIds,
    IReadOnlyList<string> DeactivatedEntityIds, IReadOnlyList<string> ChangedRegionIds);

/// <summary>Promotes configured regional wildlife inside a radius and demotes it outside a hysteresis band.</summary>
public sealed class WildlifeProximityCoordinator
{
    private readonly World _world;
    private readonly SpeciesCatalog _speciesCatalog;
    private readonly WildlifeRegionCatalog _regions;
    private readonly NearFarTransitionService _transitions;
    private readonly HashSet<string> _activeRegionIds = new(StringComparer.Ordinal);

    public WildlifeProximityCoordinator(World world, SpeciesCatalog speciesCatalog, WildlifeRegionCatalog regions)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _speciesCatalog = speciesCatalog ?? throw new ArgumentNullException(nameof(speciesCatalog));
        _regions = regions ?? throw new ArgumentNullException(nameof(regions));
        _transitions = new NearFarTransitionService(world, speciesCatalog);
        foreach (var region in regions.Regions)
            if (HasRestorableActiveMembership(world, region))
                _activeRegionIds.Add(region.Id);
    }

    public WildlifeProximityUpdate Update(Vector3 playerPosition)
    {
        if (!float.IsFinite(playerPosition.X) || !float.IsFinite(playerPosition.Y) || !float.IsFinite(playerPosition.Z))
            throw new ArgumentOutOfRangeException(nameof(playerPosition));
        var activated = new List<string>();
        var deactivated = new List<string>();
        var changed = new List<string>();
        foreach (var region in _regions.Regions)
        {
            var distance = HorizontalDistance(playerPosition, region.Center);
            if (!_activeRegionIds.Contains(region.Id) && distance <= region.ActivationRadius)
            {
                var regionEntityIds = new List<string>();
                var originalFarRecords = region.Species
                    .Select(entry => _world.TryGetFarPopulation(region.Id, entry.SpeciesId, out var record) ? record : null)
                    .Where(record => record is not null)
                    .Cast<FarEntityRecord>()
                    .ToArray();
                try
                {
                    foreach (var species in region.Species)
                    {
                        if (!_world.TryGetFarPopulation(region.Id, species.SpeciesId, out var record) || record is null || record.Count == 0)
                            continue;
                        var positions = species.SpawnPositions.Take(record.Count).ToArray();
                        if (positions.Length != record.Count)
                            throw new InvalidOperationException($"Wildlife region '{region.Id}' does not have enough '{species.SpeciesId}' spawn anchors.");
                        var entities = _transitions.Promote(region.Id, species.SpeciesId, positions);
                        foreach (var entity in entities)
                        {
                            if (_world.SetWildTerritory(entity.Id, entity.Position, NewGameSession.StarterCreatureTerritoryRadius)
                                is not (WildTerritoryResult.Accepted or WildTerritoryResult.Unchanged))
                                throw new InvalidOperationException($"Unable to assign territory to wildlife '{entity.Id}'.");
                            regionEntityIds.Add(entity.Id);
                        }
                    }
                }
                catch
                {
                    foreach (var near in _world.NearPopulations.Where(record => record.RegionId == region.Id).ToArray())
                    {
                        var entityIds = near.EntityIds.ToArray();
                        _world.RemoveNearPopulation(region.Id, near.SpeciesId);
                        foreach (var entityId in entityIds) _world.TryRemoveEntity(entityId);
                    }
                    foreach (var record in originalFarRecords) _world.SetFarPopulation(record);
                    throw;
                }
                _activeRegionIds.Add(region.Id);
                activated.AddRange(regionEntityIds);
                changed.Add(region.Id);
            }
            else if (_activeRegionIds.Contains(region.Id) && distance > region.DeactivationRadius)
            {
                foreach (var species in region.Species)
                {
                    var before = _world.NearPopulations.FirstOrDefault(near => near.RegionId == region.Id && near.SpeciesId == species.SpeciesId);
                    if (before is null) continue;
                    var ids = before.EntityIds.ToArray();
                    if (_transitions.Demote(region.Id, species.SpeciesId))
                        deactivated.AddRange(ids.Where(id => !_world.TryGetEntity(id, out _)));
                }
                _activeRegionIds.Remove(region.Id);
                changed.Add(region.Id);
            }
        }
        return new WildlifeProximityUpdate(Array.AsReadOnly(activated.ToArray()),
            Array.AsReadOnly(deactivated.ToArray()), Array.AsReadOnly(changed.ToArray()));
    }

    private static float HorizontalDistance(Vector3 left, Vector3 right)
    {
        var x = left.X - right.X;
        var z = left.Z - right.Z;
        return MathF.Sqrt(x * x + z * z);
    }

    private static bool HasRestorableActiveMembership(World world, WildlifeRegionDefinition region)
    {
        var hasActiveMembership = false;
        foreach (var species in region.Species)
        {
            if (world.TryGetNearPopulation(region.Id, species.SpeciesId, out _))
            {
                hasActiveMembership = true;
                continue;
            }

            // Extinct species have a durable zero-count far record rather than an empty near
            // membership. Treat that record as accounted for while restoring the region's
            // active state from a save containing other, still-active species.
            if (!world.TryGetFarPopulation(region.Id, species.SpeciesId, out var far)
                || far is null || far.Count != 0)
                return false;
        }

        return hasActiveMembership;
    }
}
