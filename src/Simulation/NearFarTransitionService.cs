using System.Numerics;

namespace Simulation;

public sealed class NearFarTransitionService
{
    private readonly World _world;
    private readonly SpeciesCatalog _catalog;
    private readonly Dictionary<string, FarEntityRecord> _farByRegion = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _nearByRegion = new(StringComparer.Ordinal);

    public NearFarTransitionService(World world, SpeciesCatalog catalog)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public IReadOnlyDictionary<string, FarEntityRecord> FarRecords => _farByRegion;

    public void SetFarRecord(FarEntityRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!_catalog.TryGet(record.SpeciesId, out _)) throw new ArgumentException("Unknown species.", nameof(record));
        _farByRegion[record.RegionId] = record;
    }

    public IReadOnlyList<EntityRecord> Promote(string regionId, Vector3 position)
    {
        if (!_farByRegion.Remove(regionId, out var record)) return [];
        var entities = new List<EntityRecord>(record.Count);
        for (var index = 0; index < record.Count; index++) entities.Add(_world.SpawnEntity(record.SpeciesId, position, record.AverageSatiety));
        _nearByRegion[regionId] = entities.Select(entity => entity.Id).ToList();
        return entities;
    }

    public bool Demote(string regionId)
    {
        if (!_nearByRegion.Remove(regionId, out var entityIds)) return false;
        var entities = entityIds.Select(id => _world.TryGetEntity(id, out var entity) ? entity : null).Where(entity => entity is not null).Cast<EntityRecord>().ToArray();
        if (entities.Length == 0) return false;
        var first = entities[0];
        SetFarRecord(new FarEntityRecord(regionId, first.SpeciesId, entities.Length, entities.Average(entity => entity.Satiety), []));
        foreach (var entity in entities) _world.TryRemoveEntity(entity.Id);
        return true;
    }
}
