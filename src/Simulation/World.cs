using System.Globalization;
using System.Numerics;

namespace Simulation;

public sealed class World
{
    private readonly Dictionary<string, EntityRecord> _entities = new(StringComparer.Ordinal);
    private long _lastEntityId;

    public IReadOnlyCollection<EntityRecord> Entities => _entities.Values;

    public EntityRecord SpawnEntity(
        string speciesId,
        Vector3 position,
        double satiety = 60)
    {
        if (string.IsNullOrWhiteSpace(speciesId))
        {
            throw new ArgumentException("A species ID is required.", nameof(speciesId));
        }

        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position coordinates must be finite.");
        }

        if (!double.IsFinite(satiety))
        {
            throw new ArgumentOutOfRangeException(nameof(satiety), "Satiety must be finite.");
        }

        if (_lastEntityId == long.MaxValue)
        {
            throw new InvalidOperationException("No more entity IDs can be allocated in this world.");
        }

        var id = $"entity-{(++_lastEntityId).ToString("D6", CultureInfo.InvariantCulture)}";
        var entity = new EntityRecord(id, speciesId, position, satiety);
        _entities.Add(id, entity);
        return entity;
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
