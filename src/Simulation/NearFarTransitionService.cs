using System.Numerics;

namespace Simulation;

/// <summary>Moves one region/species population between durable far records and active World entities.</summary>
public sealed class NearFarTransitionService
{
    private readonly World _world;
    private readonly SpeciesCatalog _catalog;
    private readonly Dictionary<(string RegionId, string SpeciesId), List<string>> _nearByRegionSpecies = [];
    private readonly Dictionary<(string RegionId, string SpeciesId), IReadOnlyDictionary<string, int>> _activeResources = [];

    public NearFarTransitionService(World world, SpeciesCatalog catalog)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _world.SpeciesCatalog = catalog;
    }

    public IReadOnlyCollection<FarEntityRecord> FarRecords => _world.FarPopulations;

    public void SetFarRecord(FarEntityRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!_catalog.TryGet(record.SpeciesId, out _)) throw new ArgumentException("Unknown species.", nameof(record));
        _world.SetFarPopulation(record);
    }

    /// <summary>Promotes a region when it contains exactly one far species record.</summary>
    public IReadOnlyList<EntityRecord> Promote(string regionId, Vector3 position)
    {
        var records = _world.FarPopulations.Where(record => record.RegionId == regionId).ToArray();
        return records.Length == 1 ? Promote(regionId, records[0].SpeciesId, position) : [];
    }

    public IReadOnlyList<EntityRecord> Promote(string regionId, string speciesId, Vector3 position)
    {
        if (!_world.TryGetFarPopulation(regionId, speciesId, out var record) || record is null)
            return [];
        if (record.Count == 0) return [];
        if (_nearByRegionSpecies.ContainsKey((regionId, speciesId))) return [];

        var species = _catalog.GetRequired(speciesId);
        var entities = new List<EntityRecord>(record.Count);
        try
        {
            for (var index = 0; index < record.Count; index++)
                entities.Add(_world.SpawnEntity(species, position, record.AverageSatiety));
        }
        catch
        {
            foreach (var entity in entities) _world.TryRemoveEntity(entity.Id);
            throw;
        }

        _world.RemoveFarPopulation(regionId, speciesId);
        _nearByRegionSpecies[(regionId, speciesId)] = entities.Select(entity => entity.Id).ToList();
        _activeResources[(regionId, speciesId)] = record.Resources;
        return entities;
    }

    /// <summary>Demotes a region when exactly one species is currently represented by this service.</summary>
    public bool Demote(string regionId)
    {
        var active = _nearByRegionSpecies.Keys.Where(key => key.RegionId == regionId).ToArray();
        return active.Length == 1 && Demote(regionId, active[0].SpeciesId);
    }

    public bool Demote(string regionId, string speciesId)
    {
        var key = (regionId, speciesId);
        if (!_nearByRegionSpecies.TryGetValue(key, out var entityIds)) return false;

        var entities = entityIds
            .Select(id => _world.TryGetEntity(id, out var entity) ? entity : null)
            .Where(entity => entity is not null)
            .Cast<EntityRecord>()
            .ToArray();
        if (entities.Length > 0)
        {
            _world.SetFarPopulation(new FarEntityRecord(
                regionId,
                speciesId,
                entities.Length,
                entities.Average(entity => entity.Satiety),
                _activeResources.GetValueOrDefault(key) ?? new Dictionary<string, int>(StringComparer.Ordinal)));
        }

        foreach (var entity in entities) _world.TryRemoveEntity(entity.Id);
        _nearByRegionSpecies.Remove(key);
        _activeResources.Remove(key);
        return entities.Length > 0;
    }
}
