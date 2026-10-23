using System.Numerics;

namespace Simulation;

/// <summary>Moves one region/species population between durable far records and active World entities.</summary>
public sealed class NearFarTransitionService
{
    private readonly World _world;
    private readonly SpeciesCatalog _catalog;

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
        return record.Count == 0 ? [] : Promote(regionId, speciesId,
            Enumerable.Repeat(position, record.Count).ToArray());
    }

    public IReadOnlyList<EntityRecord> Promote(string regionId, string speciesId, IReadOnlyList<Vector3> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        if (!_world.TryGetFarPopulation(regionId, speciesId, out var record) || record is null || record.Count == 0)
            return [];
        if (positions.Count != record.Count || positions.Any(position => !float.IsFinite(position.X)
            || !float.IsFinite(position.Y) || !float.IsFinite(position.Z)))
            throw new ArgumentException("A finite spawn position is required for every far entity.", nameof(positions));
        if (_world.TryGetNearPopulation(regionId, speciesId, out _)) return [];
        var entities = new List<EntityRecord>(record.Count);
        try
        {
            var species = _catalog.GetRequired(speciesId);
            for (var index = 0; index < positions.Count; index++)
                entities.Add(_world.SpawnEntity(species, positions[index], record.AverageSatiety));
            _world.RemoveFarPopulation(regionId, speciesId);
            _world.SetNearPopulation(new NearPopulationRecord(regionId, speciesId,
                entities.Select(entity => entity.Id), record.Resources));
            return entities;
        }
        catch
        {
            _world.RemoveNearPopulation(regionId, speciesId);
            foreach (var entity in entities) _world.TryRemoveEntity(entity.Id);
            if (!_world.TryGetFarPopulation(regionId, speciesId, out _))
                _world.SetFarPopulation(record);
            throw;
        }
    }

    /// <summary>Demotes a region when exactly one species is currently represented by this service.</summary>
    public bool Demote(string regionId)
    {
        var active = _world.NearPopulations.Where(record => record.RegionId == regionId).ToArray();
        return active.Length == 1 && Demote(regionId, active[0].SpeciesId);
    }

    public bool Demote(string regionId, string speciesId)
    {
        if (!_world.TryGetNearPopulation(regionId, speciesId, out var near) || near is null) return false;

        var entities = near.EntityIds
            .Select(id => _world.TryGetEntity(id, out var entity) ? entity : null)
            .Where(entity => entity is not null)
            .Cast<EntityRecord>()
            .ToArray();
        var survivors = entities.Where(entity => !entity.Health.IsDead).ToArray();
        _world.RemoveNearPopulation(regionId, speciesId);
        _world.SetFarPopulation(new FarEntityRecord(regionId, speciesId, survivors.Length,
            survivors.Length > 0 ? survivors.Average(entity => entity.Satiety) : 0, near.Resources));
        foreach (var entity in entities) _world.TryRemoveEntity(entity.Id);
        return true;
    }
}
