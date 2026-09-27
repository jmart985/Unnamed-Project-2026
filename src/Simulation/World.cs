using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;

namespace Simulation;

public sealed class World
{
    public const int DefaultEntityInventoryCapacity = 10;
    public const int DefaultStockpileCapacity = 100;

    private readonly Dictionary<string, EntityRecord> _entities = new(StringComparer.Ordinal);
    private readonly List<GroundPile> _groundPiles = [];
    private readonly ReadOnlyCollection<GroundPile> _groundPileView;
    private readonly int _entityInventoryCapacity;
    private long _lastEntityId;

    public World()
        : this(ItemCatalog.Default)
    {
    }

    public World(
        ItemCatalog itemCatalog,
        int entityInventoryCapacity = DefaultEntityInventoryCapacity,
        int stockpileCapacity = DefaultStockpileCapacity)
    {
        ArgumentNullException.ThrowIfNull(itemCatalog);
        ArgumentOutOfRangeException.ThrowIfNegative(entityInventoryCapacity);
        ArgumentOutOfRangeException.ThrowIfNegative(stockpileCapacity);

        ItemCatalog = itemCatalog;
        _entityInventoryCapacity = entityInventoryCapacity;
        Stockpile = new Stockpile(itemCatalog, stockpileCapacity);
        _groundPileView = _groundPiles.AsReadOnly();
    }

    public ItemCatalog ItemCatalog { get; }
    public IReadOnlyCollection<EntityRecord> Entities => _entities.Values;
    public IReadOnlyList<GroundPile> GroundPiles => _groundPileView;
    public Stockpile Stockpile { get; }

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
        var inventory = new ItemContainer(ItemCatalog, _entityInventoryCapacity);
        var entity = new EntityRecord(id, speciesId, position, satiety, inventory);
        _entities.Add(id, entity);
        return entity;
    }

    public GroundPile AddGroundPile(Vector3 position, int capacity)
    {
        var pile = new GroundPile(ItemCatalog, capacity, position);
        _groundPiles.Add(pile);
        return pile;
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
        _groundPiles.Clear();
        Stockpile.Clear();
    }
}
