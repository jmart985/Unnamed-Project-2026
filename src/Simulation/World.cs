using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;

namespace Simulation;

public sealed class World
{
    public const int DefaultInventoryCapacity = 10;

    private readonly Dictionary<string, EntityRecord> _entities = new(StringComparer.Ordinal);
    private readonly Dictionary<(string ObserverId, string TargetId), RelationshipState> _relationships = [];
    private readonly HashSet<string> _factionIds = new(StringComparer.Ordinal);
    private readonly Dictionary<(string EntityId, string FactionId), double> _factionReputations = [];
    private readonly Dictionary<(string SourceFactionId, string TargetFactionId), bool> _factionHostilities = [];
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
        int inventoryCapacity = DefaultInventoryCapacity,
        CharacterStats? defaultCharacterStats = null)
    {
        if (inventoryCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inventoryCapacity), "Inventory capacity cannot be negative.");
        }

        NeedsTuning = needsTuning ?? new NeedsTuning();
        InventoryCapacity = inventoryCapacity;
        DefaultCharacterStats = defaultCharacterStats ?? CharacterStats.Default;
        Reservations = new ReservationService(IsActionActive);
        _foodSourceView = _foodSources.AsReadOnly();
        _resourcePileView = _resourcePiles.AsReadOnly();
        _stockpileView = _stockpiles.AsReadOnly();
    }

    public IReadOnlyCollection<EntityRecord> Entities => _entities.Values;
    public IReadOnlyCollection<string> FactionIds =>
        Array.AsReadOnly(_factionIds.Order(StringComparer.Ordinal).ToArray());
    public IReadOnlyCollection<FactionReputationState> FactionReputations =>
        Array.AsReadOnly(_factionReputations
            .OrderBy(pair => pair.Key.EntityId, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.FactionId, StringComparer.Ordinal)
            .Select(pair => new FactionReputationState(pair.Key.EntityId, pair.Key.FactionId, pair.Value))
            .ToArray());
    public IReadOnlyCollection<FactionHostilityState> FactionHostilities =>
        Array.AsReadOnly(_factionHostilities
            .OrderBy(pair => pair.Key.SourceFactionId, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.TargetFactionId, StringComparer.Ordinal)
            .Select(pair => new FactionHostilityState(
                pair.Key.SourceFactionId,
                pair.Key.TargetFactionId,
                pair.Value))
            .ToArray());
    public IReadOnlyCollection<RelationshipState> Relationships =>
        Array.AsReadOnly(_relationships.Values.ToArray());
    public IReadOnlyList<FoodSource> FoodSources => _foodSourceView;
    internal long LastAllocatedEntityId => _lastEntityId;
    public IReadOnlyList<ResourceLocation> ResourcePiles => _resourcePileView;
    public IReadOnlyList<ResourceLocation> Stockpiles => _stockpileView;
    public NeedsTuning NeedsTuning { get; }
    public ReservationService Reservations { get; }
    public int InventoryCapacity { get; }
    public CharacterStats DefaultCharacterStats { get; }
    public int? SessionSeed { get; private set; }
    public string? PlayerEntityId { get; private set; }
    public PlayerCharacterProfile? PlayerProfile { get; private set; }

    internal void SetSessionPlayer(
        int seed,
        string playerEntityId,
        PlayerCharacterProfile profile)
    {
        if (seed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seed), "The session seed cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.IsValid)
        {
            throw new ArgumentException("The player profile is invalid.", nameof(profile));
        }

        if (!TryGetEntity(playerEntityId, out var player)
            || player is null
            || !string.Equals(player.SpeciesId, "human", StringComparison.Ordinal))
        {
            throw new ArgumentException("A live human entity is required as the session player.", nameof(playerEntityId));
        }

        SessionSeed = seed;
        PlayerEntityId = playerEntityId;
        PlayerProfile = profile;
    }

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
        return SpawnEntityCore(speciesId, position, satiety, DefaultCharacterStats);
    }

    public EntityRecord SpawnEntity(
        SpeciesDefinition species,
        Vector3 position,
        double satiety = 60)
    {
        ArgumentNullException.ThrowIfNull(species);
        return SpawnEntityCore(
            species.Id,
            position,
            satiety,
            species.CreateCharacterStats(DefaultCharacterStats));
    }

    private EntityRecord SpawnEntityCore(
        string speciesId,
        Vector3 position,
        double satiety,
        CharacterStats baseStats)
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
            Reservations,
            baseStats);
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

    public CreatureCaptureResult TryCaptureCreature(string? entityId)
    {
        if (!TryGetEntity(entityId, out var entity) || entity is null)
        {
            return CreatureCaptureResult.TargetNotFound;
        }

        if (string.Equals(entity.SpeciesId, "human", StringComparison.Ordinal))
        {
            return CreatureCaptureResult.NotCreature;
        }

        if (entity.Health.IsDead)
        {
            return CreatureCaptureResult.TargetDead;
        }

        if (entity.CaptivityState == CreatureCaptivityState.Captive)
        {
            return CreatureCaptureResult.AlreadyCaptive;
        }

        entity.CaptivityState = CreatureCaptivityState.Captive;
        return CreatureCaptureResult.Captured;
    }

    public FactionDefinitionResult DefineFaction(string? factionId)
    {
        if (!IsValidFactionId(factionId))
        {
            return FactionDefinitionResult.InvalidId;
        }

        return _factionIds.Add(factionId!)
            ? FactionDefinitionResult.Defined
            : FactionDefinitionResult.DuplicateId;
    }

    public FactionAllegianceResult SetEntityAllegiance(string? entityId, string? factionId)
    {
        if (!TryGetEntity(entityId, out var entity) || entity is null)
        {
            return FactionAllegianceResult.EntityNotFound;
        }

        if (factionId is null)
        {
            entity.AllegianceFactionId = null;
            return FactionAllegianceResult.Removed;
        }

        if (!_factionIds.Contains(factionId))
        {
            return FactionAllegianceResult.UnknownFaction;
        }

        entity.AllegianceFactionId = factionId;
        return FactionAllegianceResult.Assigned;
    }

    public bool TryGetFactionReputation(
        string? entityId,
        string? factionId,
        out double reputation)
    {
        reputation = 0;
        if (string.IsNullOrWhiteSpace(entityId)
            || string.IsNullOrWhiteSpace(factionId)
            || !_entities.ContainsKey(entityId)
            || !_factionIds.Contains(factionId))
        {
            return false;
        }

        return _factionReputations.TryGetValue((entityId, factionId), out reputation);
    }

    public FactionReputationResult AdjustFactionReputation(
        string? entityId,
        string? factionId,
        double delta)
    {
        if (string.IsNullOrWhiteSpace(entityId) || !_entities.ContainsKey(entityId))
        {
            return FactionReputationResult.EntityNotFound;
        }

        if (string.IsNullOrWhiteSpace(factionId) || !_factionIds.Contains(factionId))
        {
            return FactionReputationResult.UnknownFaction;
        }

        if (!double.IsFinite(delta))
        {
            return FactionReputationResult.InvalidDelta;
        }

        var key = (entityId, factionId);
        _factionReputations.TryGetValue(key, out var currentValue);
        var updatedValue = currentValue + delta;
        if (updatedValue is < 0 or > 100 || !double.IsFinite(updatedValue))
        {
            return FactionReputationResult.OutOfRange;
        }

        _factionReputations[key] = updatedValue;
        return FactionReputationResult.Applied;
    }

    public bool TryGetFactionHostility(
        string? sourceFactionId,
        string? targetFactionId,
        out bool isHostile)
    {
        isHostile = false;
        if (string.IsNullOrWhiteSpace(sourceFactionId)
            || string.IsNullOrWhiteSpace(targetFactionId)
            || string.Equals(sourceFactionId, targetFactionId, StringComparison.Ordinal)
            || !_factionIds.Contains(sourceFactionId)
            || !_factionIds.Contains(targetFactionId))
        {
            return false;
        }

        return _factionHostilities.TryGetValue((sourceFactionId, targetFactionId), out isHostile);
    }

    public FactionHostilityResult SetFactionHostility(
        string? sourceFactionId,
        string? targetFactionId,
        bool isHostile)
    {
        if (string.IsNullOrWhiteSpace(sourceFactionId)
            || string.IsNullOrWhiteSpace(targetFactionId)
            || !_factionIds.Contains(sourceFactionId)
            || !_factionIds.Contains(targetFactionId))
        {
            return FactionHostilityResult.UnknownFaction;
        }

        if (string.Equals(sourceFactionId, targetFactionId, StringComparison.Ordinal))
        {
            return FactionHostilityResult.SameFaction;
        }

        _factionHostilities[(sourceFactionId, targetFactionId)] = isHostile;
        return FactionHostilityResult.Updated;
    }

    public bool TryGetRelationship(
        string? observerEntityId,
        string? targetEntityId,
        out RelationshipState? relationship)
    {
        relationship = null;
        if (string.IsNullOrWhiteSpace(observerEntityId)
            || string.IsNullOrWhiteSpace(targetEntityId)
            || string.Equals(observerEntityId, targetEntityId, StringComparison.Ordinal)
            || !TryGetEntity(observerEntityId, out _)
            || !TryGetEntity(targetEntityId, out _))
        {
            return false;
        }

        return _relationships.TryGetValue(
            (observerEntityId!, targetEntityId!),
            out relationship);
    }

    public bool TryGetOrCreateRelationship(
        string? observerEntityId,
        string? targetEntityId,
        out RelationshipState? relationship)
    {
        if (string.IsNullOrWhiteSpace(observerEntityId)
            || string.IsNullOrWhiteSpace(targetEntityId)
            || string.Equals(observerEntityId, targetEntityId, StringComparison.Ordinal)
            || !TryGetEntity(observerEntityId, out _)
            || !TryGetEntity(targetEntityId, out _))
        {
            relationship = null;
            return false;
        }

        var key = (observerEntityId!, targetEntityId!);
        if (_relationships.TryGetValue(key, out relationship))
        {
            return true;
        }

        relationship = new RelationshipState(key.Item1, key.Item2);
        _relationships.Add(key, relationship);
        return true;
    }

    public RelationshipAdjustmentResult ApplyRelationshipAdjustment(
        string? observerEntityId,
        string? targetEntityId,
        RelationshipAdjustment? adjustment)
    {
        if (adjustment is null
            || !double.IsFinite(adjustment.FamiliarityDelta)
            || !double.IsFinite(adjustment.TrustDelta)
            || !double.IsFinite(adjustment.FearDelta)
            || !double.IsFinite(adjustment.LoyaltyDelta))
        {
            return RelationshipAdjustmentResult.InvalidDelta;
        }

        if (!TryGetOrCreateRelationship(observerEntityId, targetEntityId, out var relationship)
            || relationship is null)
        {
            return RelationshipAdjustmentResult.InvalidParticipant;
        }

        relationship.ApplyDelta(RelationshipDimension.Familiarity, adjustment.FamiliarityDelta);
        relationship.ApplyDelta(RelationshipDimension.Trust, adjustment.TrustDelta);
        relationship.ApplyDelta(RelationshipDimension.Fear, adjustment.FearDelta);
        relationship.ApplyDelta(RelationshipDimension.Loyalty, adjustment.LoyaltyDelta);
        return RelationshipAdjustmentResult.Applied;
    }

    public bool TryRemoveEntity(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !_entities.TryGetValue(id, out var entity))
        {
            return false;
        }

        entity.Actions.Cancel();
        Reservations.ReleaseForEntity(id);
        RemoveRelationshipsForEntity(id);
        RemoveFactionReputationsForEntity(id);
        entity.AllegianceFactionId = null;
        if (!_entities.Remove(id))
        {
            return false;
        }

        if (string.Equals(id, PlayerEntityId, StringComparison.Ordinal))
        {
            PlayerEntityId = null;
            PlayerProfile = null;
        }

        return true;
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
            entity.AllegianceFactionId = null;
        }

        _entities.Clear();
        _relationships.Clear();
        _factionIds.Clear();
        _factionReputations.Clear();
        _factionHostilities.Clear();
        _foodSources.Clear();
        _resourcePiles.Clear();
        _stockpiles.Clear();
        _targetIds.Clear();
        Reservations.Reset();
        SessionSeed = null;
        PlayerEntityId = null;
        PlayerProfile = null;
    }

    internal void ApplySnapshot(WorldSnapshot snapshot, SpeciesCatalog speciesCatalog)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(speciesCatalog);

        Reset();
        foreach (var entity in snapshot.Entities)
        {
            var species = speciesCatalog.GetRequired(entity.SpeciesId);
            var restored = SpawnEntityWithId(
                entity.Id,
                entity.SpeciesId,
                entity.Position,
                entity.Satiety,
                species.CreateCharacterStats(DefaultCharacterStats));
            restored.CaptivityState = entity.CaptivityState;
            foreach (var item in entity.Inventory)
            {
                restored.TryAddCarried(item.Key, item.Value);
            }
        }

        foreach (var factionId in snapshot.FactionIds!)
        {
            DefineFaction(factionId);
        }

        foreach (var entity in snapshot.Entities)
        {
            if (entity.AllegianceFactionId is { } factionId)
            {
                SetEntityAllegiance(entity.Id, factionId);
            }
        }

        foreach (var reputation in snapshot.FactionReputations!)
        {
            _factionReputations.Add((reputation.EntityId, reputation.FactionId), reputation.Value);
        }

        foreach (var hostility in snapshot.FactionHostilities!)
        {
            _factionHostilities.Add(
                (hostility.SourceFactionId, hostility.TargetFactionId),
                hostility.IsHostile);
        }

        foreach (var source in snapshot.FoodSources)
        {
            var contents = new ResourceContainer(
                source.Contents.Name,
                source.Contents.Capacity);
            foreach (var item in source.Contents.Counts)
            {
                contents.AddKnown(item.Key, item.Value);
            }

            AddFoodSource(source.Id, source.Position, contents);
        }

        foreach (var pile in snapshot.ResourcePiles)
        {
            AddResourcePile(pile.Id, pile.Position, RestoreContainer(pile.Contents));
        }

        foreach (var stockpile in snapshot.Stockpiles)
        {
            AddStockpile(stockpile.Id, stockpile.Position, RestoreContainer(stockpile.Contents));
        }

        foreach (var savedRelationship in snapshot.Relationships)
        {
            if (!TryGetOrCreateRelationship(
                savedRelationship.ObserverEntityId,
                savedRelationship.TargetEntityId,
                out var relationship)
                || relationship is null)
            {
                throw new InvalidOperationException(
                    "A validated relationship could not be restored into the world.");
            }

            relationship.ApplyDelta(RelationshipDimension.Familiarity, savedRelationship.Familiarity);
            relationship.ApplyDelta(RelationshipDimension.Trust, savedRelationship.Trust);
            relationship.ApplyDelta(RelationshipDimension.Fear, savedRelationship.Fear);
            relationship.ApplyDelta(RelationshipDimension.Loyalty, savedRelationship.Loyalty);
        }

        _lastEntityId = snapshot.LastAllocatedEntityId;
        if (snapshot.SessionSeed is { } seed
            && snapshot.PlayerEntityId is { } playerEntityId
            && snapshot.PlayerProfile is { } playerProfile)
        {
            SetSessionPlayer(seed, playerEntityId, playerProfile);
        }
    }

    internal static bool IsValidFactionId(string? factionId)
    {
        if (string.IsNullOrWhiteSpace(factionId)
            || factionId[0] == '-'
            || factionId[^1] == '-')
        {
            return false;
        }

        var previousWasSeparator = false;
        foreach (var character in factionId)
        {
            if (character == '-')
            {
                if (previousWasSeparator)
                {
                    return false;
                }

                previousWasSeparator = true;
                continue;
            }

            if (character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9'))
            {
                return false;
            }

            previousWasSeparator = false;
        }

        return true;
    }

    private static ResourceContainer RestoreContainer(ResourceContainerSnapshot snapshot)
    {
        var contents = new ResourceContainer(snapshot.Name, snapshot.Capacity);
        foreach (var item in snapshot.Counts)
        {
            contents.AddKnown(item.Key, item.Value);
        }

        return contents;
    }

    private EntityRecord SpawnEntityWithId(
        string id,
        string speciesId,
        Vector3 position,
        double satiety,
        CharacterStats baseStats)
    {
        var entity = new EntityRecord(
            id,
            speciesId,
            position,
            satiety,
            InventoryCapacity,
            NeedsTuning,
            Reservations,
            baseStats);
        _entities.Add(id, entity);
        return entity;
    }

    private void AddTargetId(string id, string parameterName)
    {
        if (!_targetIds.Add(id))
        {
            throw new ArgumentException($"Resource target ID '{id}' has already been used in this world.", parameterName);
        }
    }

    private void RemoveRelationshipsForEntity(string entityId)
    {
        var relatedPairs = _relationships.Keys
            .Where(pair => string.Equals(pair.ObserverId, entityId, StringComparison.Ordinal)
                || string.Equals(pair.TargetId, entityId, StringComparison.Ordinal))
            .ToArray();
        foreach (var pair in relatedPairs)
        {
            _relationships.Remove(pair);
        }
    }

    private void RemoveFactionReputationsForEntity(string entityId)
    {
        var reputationKeys = _factionReputations.Keys
            .Where(key => string.Equals(key.EntityId, entityId, StringComparison.Ordinal))
            .ToArray();
        foreach (var key in reputationKeys)
        {
            _factionReputations.Remove(key);
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
