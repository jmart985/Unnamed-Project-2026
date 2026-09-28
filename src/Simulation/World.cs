using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;

namespace Simulation;

public sealed class World
{
    public const int DefaultInventoryCapacity = 10;

    private readonly Dictionary<string, EntityRecord> _entities = new(StringComparer.Ordinal);
    private readonly List<FoodSource> _foodSources = [];
    private readonly ReadOnlyCollection<FoodSource> _foodSourceView;
    private readonly List<ResourceLocation> _resourcePiles = [];
    private readonly ReadOnlyCollection<ResourceLocation> _resourcePileView;
    private readonly List<ResourceLocation> _stockpiles = [];
    private readonly ReadOnlyCollection<ResourceLocation> _stockpileView;
    private readonly HashSet<string> _targetIds = new(StringComparer.Ordinal);
    private long _lastEntityId;

    public World(
        NeedsTuning? needsTuning = null,
        int inventoryCapacity = DefaultInventoryCapacity)
    {
        if (inventoryCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inventoryCapacity), "Inventory capacity cannot be negative.");
        }

        NeedsTuning = needsTuning ?? new NeedsTuning();
        InventoryCapacity = inventoryCapacity;
        Reservations = new ReservationService(IsActionActive);
        _foodSourceView = _foodSources.AsReadOnly();
        _resourcePileView = _resourcePiles.AsReadOnly();
        _stockpileView = _stockpiles.AsReadOnly();
    }

    public IReadOnlyCollection<EntityRecord> Entities => _entities.Values;
    public IReadOnlyList<FoodSource> FoodSources => _foodSourceView;
    internal long LastAllocatedEntityId => _lastEntityId;
    public IReadOnlyList<ResourceLocation> ResourcePiles => _resourcePileView;
    public IReadOnlyList<ResourceLocation> Stockpiles => _stockpileView;
    public NeedsTuning NeedsTuning { get; }
    public ReservationService Reservations { get; }
    public int InventoryCapacity { get; }

    public FoodSource AddFoodSource(string id, Vector3 position, ResourceContainer contents)
    {
        var source = new FoodSource(id, position, contents);
        AddTargetId(source.Id, nameof(id));

        _foodSources.Add(source);
        return source;
    }

    public ResourceLocation AddResourcePile(string id, Vector3 position, ResourceContainer contents)
    {
        var pile = new ResourceLocation(id, position, contents);
        AddTargetId(pile.Id, nameof(id));
        _resourcePiles.Add(pile);
        return pile;
    }

    public ResourceLocation AddStockpile(string id, Vector3 position, ResourceContainer contents)
    {
        var stockpile = new ResourceLocation(id, position, contents);
        AddTargetId(stockpile.Id, nameof(id));
        _stockpiles.Add(stockpile);
        return stockpile;
    }

    public bool TryGetFoodSource(string? id, out FoodSource? source)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            source = _foodSources.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, id, StringComparison.Ordinal));
            return source is not null;
        }

        source = null;
        return false;
    }

    public bool TryRemoveFoodSource(string? id)
    {
        if (!TryGetFoodSource(id, out var source) || source is null)
        {
            return false;
        }

        _foodSources.Remove(source);
        Reservations.RemoveTarget(source.Id);
        return true;
    }

    public bool TryRemoveResourcePile(string? id)
    {
        var pile = FindTarget(_resourcePiles, id);
        if (pile is null)
        {
            return false;
        }

        _resourcePiles.Remove(pile);
        Reservations.RemoveTarget(pile.Id);
        return true;
    }

    public bool TryRemoveStockpile(string? id)
    {
        var stockpile = FindTarget(_stockpiles, id);
        if (stockpile is null)
        {
            return false;
        }

        _stockpiles.Remove(stockpile);
        Reservations.RemoveTarget(stockpile.Id);
        return true;
    }

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
        var entity = new EntityRecord(
            id,
            speciesId,
            position,
            satiety,
            InventoryCapacity,
            NeedsTuning,
            Reservations);
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
        if (string.IsNullOrWhiteSpace(id) || !_entities.TryGetValue(id, out var entity))
        {
            return false;
        }

        entity.Actions.Cancel();
        Reservations.ReleaseForEntity(id);
        return _entities.Remove(id);
    }

    public bool TrySetEntityPosition(string? id, Vector3 position)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position coordinates must be finite.");
        }

        if (!TryGetEntity(id, out var entity) || entity is null)
        {
            return false;
        }

        entity.Position = position;
        return true;
    }

    public void Reset()
    {
        foreach (var entity in _entities.Values)
        {
            entity.Actions.Cancel();
            Reservations.ReleaseForEntity(entity.Id);
        }

        _entities.Clear();
        _foodSources.Clear();
        _resourcePiles.Clear();
        _stockpiles.Clear();
        _targetIds.Clear();
        Reservations.Reset();
    }

    private void AddTargetId(string id, string parameterName)
    {
        if (!_targetIds.Add(id))
        {
            throw new ArgumentException($"Resource target ID '{id}' has already been used in this world.", parameterName);
        }
    }

    private static ResourceLocation? FindTarget(
        IEnumerable<ResourceLocation> targets,
        string? id)
    {
        return string.IsNullOrWhiteSpace(id)
            ? null
            : targets.FirstOrDefault(target => string.Equals(target.Id, id, StringComparison.Ordinal));
    }

    private bool IsActionActive(string entityId, string actionId)
    {
        return _entities.TryGetValue(entityId, out var entity)
            && string.Equals(entity.Actions.Current?.Id, actionId, StringComparison.Ordinal);
    }
}
