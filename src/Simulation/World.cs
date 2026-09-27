using System.Globalization;
using System.Numerics;

namespace Simulation;

public sealed class World
{
    private readonly Dictionary<string, EntityRecord> _entities = new(StringComparer.Ordinal);
    private long _lastEntityId;

    public World(NeedsTuning? needsTuning = null)
    {
        NeedsTuning = needsTuning ?? new NeedsTuning();
    }

    public IReadOnlyCollection<EntityRecord> Entities => _entities.Values;
    public NeedsTuning NeedsTuning { get; }

    public EntityRecord SpawnEntity(
        string speciesId,
        Vector3 position,
        double? satiety = null)
    {
        if (string.IsNullOrWhiteSpace(speciesId))
        {
            throw new ArgumentException("A species ID is required.", nameof(speciesId));
        }

        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position coordinates must be finite.");
        }

        var initialSatiety = satiety ?? NeedsTuning.InitialSatiety;
        if (!double.IsFinite(initialSatiety) || initialSatiety is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(satiety), "Satiety must be finite and within [0, 100].");
        }

        if (_lastEntityId == long.MaxValue)
        {
            throw new InvalidOperationException("No more entity IDs can be allocated in this world.");
        }

        var id = $"entity-{(++_lastEntityId).ToString("D6", CultureInfo.InvariantCulture)}";
        var entity = new EntityRecord(id, speciesId, position, initialSatiety, NeedsTuning);
        _entities.Add(id, entity);
        return entity;
    }

    public void AdvanceNeeds(double simulationSeconds)
    {
        if (!double.IsFinite(simulationSeconds) || simulationSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(simulationSeconds),
                "Simulation time must be finite and nonnegative.");
        }

        foreach (var entity in _entities.Values)
        {
            entity.Needs.Advance(simulationSeconds);
        }
    }

    public bool TryGetEntity(string? id, out EntityRecord? entity)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            entity = null;
            return false;
        }

        return _entities.TryGetValue(id, out entity);
    }

    public bool TryRemoveEntity(string? id)
    {
        return !string.IsNullOrWhiteSpace(id) && _entities.Remove(id);
    }

    public void Reset()
    {
        _entities.Clear();
    }
}
