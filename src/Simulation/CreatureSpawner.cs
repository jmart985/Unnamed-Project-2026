using System.Numerics;

namespace Simulation;

public sealed record CreatureSpawnRequest(string SpeciesId, string HabitatId, Vector3 Position, int Seed);

public enum CreatureSpawnStatus
{
    Spawned,
    InvalidSpecies,
    OccupiedHabitat,
    InvalidRequest
}

public sealed record CreatureSpawnResult(CreatureSpawnStatus Status, EntityRecord? Entity = null);

public sealed class CreatureSpawner
{
    private readonly World _world;
    private readonly SpeciesCatalog _speciesCatalog;
    private readonly Dictionary<string, string> _habitats = new(StringComparer.Ordinal);

    public CreatureSpawner(World world, SpeciesCatalog speciesCatalog)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _speciesCatalog = speciesCatalog ?? throw new ArgumentNullException(nameof(speciesCatalog));
    }

    public CreatureSpawnResult Spawn(CreatureSpawnRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.SpeciesId) || string.IsNullOrWhiteSpace(request.HabitatId))
        {
            return new CreatureSpawnResult(CreatureSpawnStatus.InvalidRequest);
        }

        if (!_speciesCatalog.TryGet(request.SpeciesId, out var species) || species is null)
        {
            return new CreatureSpawnResult(CreatureSpawnStatus.InvalidSpecies);
        }

        if (_habitats.ContainsKey(request.HabitatId))
        {
            return new CreatureSpawnResult(CreatureSpawnStatus.OccupiedHabitat);
        }

        var entity = _world.SpawnEntity(species.Id, request.Position);
        _habitats[request.HabitatId] = entity.Id;
        return new CreatureSpawnResult(CreatureSpawnStatus.Spawned, entity);
    }

    public bool Despawn(string? entityId)
    {
        if (!_world.TryGetEntity(entityId, out var entity) || entity is null || !_world.TryRemoveEntity(entity.Id))
        {
            return false;
        }

        var habitat = _habitats.FirstOrDefault(pair => pair.Value == entity.Id).Key;
        if (habitat is not null)
        {
            _habitats.Remove(habitat);
        }

        return true;
    }
}
