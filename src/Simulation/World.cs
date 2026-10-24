using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;

namespace Simulation;

public sealed class World
{
    public event Action<string, string, RelationshipAdjustment>? RelationshipChanged;
    public event Action<string, string, double>? FactionStandingChanged;

    // Gameplay events use unit steps; explicit adjustments remain available for setup/imports.
    public FactionReputationResult ApplyFactionStandingEvent(string? entityId, string? factionId, bool positive)
        => AdjustFactionReputation(entityId, factionId, positive ? 1 : -1);

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
    private readonly List<Structure> _structures = [];
    private readonly ReadOnlyCollection<Structure> _structureView;
    private readonly List<Workstation> _workstations = [];
    private readonly ReadOnlyCollection<Workstation> _workstationView;
    private readonly List<Farm> _farms = [];
    private readonly ReadOnlyCollection<Farm> _farmView;
    private readonly List<SettlementConstructionSite> _constructionSites = [];
    private readonly List<Fireplace> _fireplaces = [];
    public IReadOnlyList<Fireplace> Fireplaces => _fireplaces;
    private readonly ReadOnlyCollection<SettlementConstructionSite> _constructionSiteView;
    private readonly HashSet<string> _targetIds = new(StringComparer.Ordinal);
    private long _lastEntityId;
    private readonly HashSet<string> _squadMembers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NpcWorkSchedule> _workSchedules = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector3> _homePositions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WildTerritory> _wildTerritories = new(StringComparer.Ordinal);
    private readonly Dictionary<(string RegionId, string SpeciesId), FarEntityRecord> _farPopulations = [];
    private readonly Dictionary<(string RegionId, string SpeciesId), NearPopulationRecord> _nearPopulations = [];
    private readonly Dictionary<string, double> _treeRegrowthTimers = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, double> TreeRegrowthTimers => _treeRegrowthTimers;
    internal void SetTreeRegrowthTimer(string treeId, double elapsed) => _treeRegrowthTimers[treeId] = elapsed;
    internal void RemoveTreeRegrowthTimer(string treeId) => _treeRegrowthTimers.Remove(treeId);
    internal void ClearTreeRegrowthTimers() => _treeRegrowthTimers.Clear();
    public IReadOnlyCollection<NpcScheduleSetting> WorkSchedules => Array.AsReadOnly(_workSchedules
        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => new NpcScheduleSetting(pair.Key, pair.Value.StartHour, pair.Value.EndHour)).ToArray());
    public IReadOnlyCollection<NpcHomeSetting> HomePositions => Array.AsReadOnly(_homePositions
        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => new NpcHomeSetting(pair.Key, pair.Value)).ToArray());
    public IReadOnlyCollection<WildTerritory> WildTerritories => Array.AsReadOnly(_wildTerritories
        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => pair.Value).ToArray());
    public IReadOnlyCollection<FarEntityRecord> FarPopulations => Array.AsReadOnly(_farPopulations.Values
        .OrderBy(record => record.RegionId, StringComparer.Ordinal)
        .ThenBy(record => record.SpeciesId, StringComparer.Ordinal)
        .ToArray());
    public IReadOnlyCollection<NearPopulationRecord> NearPopulations => Array.AsReadOnly(_nearPopulations.Values
        .OrderBy(record => record.RegionId, StringComparer.Ordinal)
        .ThenBy(record => record.SpeciesId, StringComparer.Ordinal)
        .ToArray());

    public void SetFarPopulation(FarEntityRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (SpeciesCatalog is null || !SpeciesCatalog.TryGet(record.SpeciesId, out _))
            throw new ArgumentException($"Far population species '{record.SpeciesId}' is not defined in this world.", nameof(record));
        if (_nearPopulations.ContainsKey((record.RegionId, record.SpeciesId)))
            throw new InvalidOperationException("A population cannot be far and near at the same time.");
        _farPopulations[(record.RegionId, record.SpeciesId)] = record;
    }

    public void SetNearPopulation(NearPopulationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (SpeciesCatalog is null || !SpeciesCatalog.TryGet(record.SpeciesId, out _))
            throw new ArgumentException($"Near population species '{record.SpeciesId}' is not defined in this world.", nameof(record));
        var key = (record.RegionId, record.SpeciesId);
        if (_farPopulations.ContainsKey(key))
            throw new InvalidOperationException("Remove the far aggregate before activating its near entities.");
        if (_nearPopulations.ContainsKey(key))
            throw new InvalidOperationException("This regional species already has active membership.");
        foreach (var id in record.EntityIds)
        {
            if (!TryGetEntity(id, out var entity) || entity is null || entity.SpeciesId != record.SpeciesId)
                throw new ArgumentException($"Near population entity '{id}' must exist and match its species.", nameof(record));
            if (_nearPopulations.Any(pair => pair.Key != key && pair.Value.EntityIds.Contains(id, StringComparer.Ordinal)))
                throw new ArgumentException($"Near population entity '{id}' is already assigned to another population.", nameof(record));
        }
        _nearPopulations[key] = record;
    }

    public bool TryGetNearPopulation(string? regionId, string? speciesId, out NearPopulationRecord? record)
    {
        if (string.IsNullOrWhiteSpace(regionId) || string.IsNullOrWhiteSpace(speciesId))
        {
            record = null;
            return false;
        }
        return _nearPopulations.TryGetValue((regionId, speciesId), out record);
    }

    public bool RemoveNearPopulation(string? regionId, string? speciesId) =>
        !string.IsNullOrWhiteSpace(regionId) && !string.IsNullOrWhiteSpace(speciesId)
        && _nearPopulations.Remove((regionId, speciesId));

    public bool TryGetFarPopulation(string? regionId, string? speciesId, out FarEntityRecord? record)
    {
        if (string.IsNullOrWhiteSpace(regionId) || string.IsNullOrWhiteSpace(speciesId))
        {
            record = null;
            return false;
        }

        return _farPopulations.TryGetValue((regionId, speciesId), out record);
    }

    public bool RemoveFarPopulation(string? regionId, string? speciesId) =>
        !string.IsNullOrWhiteSpace(regionId) && !string.IsNullOrWhiteSpace(speciesId)
        && _farPopulations.Remove((regionId, speciesId));

    public bool TryGetWildTerritory(string? entityId, out WildTerritory? territory) =>
        _wildTerritories.TryGetValue(entityId ?? string.Empty, out territory);

    public WildTerritoryResult SetWildTerritory(string? entityId, Vector3 center, float radius)
    {
        if (!TryGetEntity(entityId, out var entity) || entity is null)
            return WildTerritoryResult.InvalidEntity;
        if (!WildTerritory.IsValidGeometry(center, radius))
            return WildTerritoryResult.InvalidGeometry;

        var territory = new WildTerritory(entity.Id, center, radius);
        if (_wildTerritories.TryGetValue(entity.Id, out var current) && current == territory)
            return WildTerritoryResult.Unchanged;

        _wildTerritories[entity.Id] = territory;
        return WildTerritoryResult.Accepted;
    }

    public bool RemoveWildTerritory(string? entityId) =>
        !string.IsNullOrWhiteSpace(entityId) && _wildTerritories.Remove(entityId);

    public bool HasDailySchedule(EntityRecord entity) => entity.SpeciesId == "human" && entity.Id != PlayerEntityId
        && TryGetEntity(entity.Id, out var owned) && ReferenceEquals(entity, owned);

    public NpcWorkSchedule GetWorkSchedule(string entityId) => _workSchedules.GetValueOrDefault(entityId, NpcWorkSchedule.Default);
    public bool TryGetHomePosition(string? entityId, out Vector3 position) => _homePositions.TryGetValue(entityId ?? string.Empty, out position);

    public ScheduleUpdateResult SetHomePosition(string? id, Vector3 position)
    {
        if (!TryGetEntity(id, out var entity) || entity is null) return ScheduleUpdateResult.InvalidEntity;
        if (!HasDailySchedule(entity) || entity.Health.IsDead) return ScheduleUpdateResult.IneligibleEntity;
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z)) return ScheduleUpdateResult.InvalidHours;
        _homePositions[entity.Id] = position;
        return ScheduleUpdateResult.Accepted;
    }

    public ScheduleUpdateResult SetWorkSchedule(string? id, int startHour, int endHour)
    {
        if (!TryGetEntity(id, out var entity) || entity is null) return ScheduleUpdateResult.InvalidEntity;
        if (!HasDailySchedule(entity) || entity.Health.IsDead) return ScheduleUpdateResult.IneligibleEntity;
        var schedule = new NpcWorkSchedule(startHour, endHour);
        if (!schedule.IsValid) return ScheduleUpdateResult.InvalidHours;
        if (GetWorkSchedule(entity.Id) == schedule) return ScheduleUpdateResult.Unchanged;
        if (schedule == NpcWorkSchedule.Default) _workSchedules.Remove(entity.Id);
        else _workSchedules[entity.Id] = schedule;
        return ScheduleUpdateResult.Accepted;
    }
    public IReadOnlyCollection<string> SquadMemberIds => Array.AsReadOnly(_squadMembers.Order(StringComparer.Ordinal).ToArray());

    public SquadResult AddSquadMember(string? id)
    {
        if (!TryGetEntity(id, out var entity) || entity is null) return SquadResult.InvalidEntity;
        if (entity.SpeciesId != "human" || entity.Id == PlayerEntityId || entity.Health.IsDead) return SquadResult.IneligibleEntity;
        return _squadMembers.Add(entity.Id) ? SquadResult.Accepted : SquadResult.Unchanged;
    }

    public SquadResult RemoveSquadMember(string? id)
    {
        if (!TryGetEntity(id, out var entity) || entity is null) return SquadResult.InvalidEntity;
        if (!_squadMembers.Remove(entity.Id)) return SquadResult.NotMember;
        if (entity.Actions.Current?.Kind == ActionKind.SquadMove)
        {
            entity.Actions.Cancel();
            entity.Activity = "Idle";
            entity.WaitingReason = null;
        }
        return SquadResult.Accepted;
    }

    public World(
        NeedsTuning? needsTuning = null,
        CharacterStats? defaultCharacterStats = null)
    {
        NeedsTuning = needsTuning ?? new NeedsTuning();
        DefaultCharacterStats = defaultCharacterStats ?? CharacterStats.Default;
        Reservations = new ReservationService(IsActionActive);
        _foodSourceView = _foodSources.AsReadOnly();
        _resourcePileView = _resourcePiles.AsReadOnly();
        _stockpileView = _stockpiles.AsReadOnly();
        _structureView = _structures.AsReadOnly();
        _workstationView = _workstations.AsReadOnly();
        _farmView = _farms.AsReadOnly();
        _constructionSiteView = _constructionSites.AsReadOnly();
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
    public IReadOnlyList<Structure> Structures => _structureView;
    public IReadOnlyList<Workstation> Workstations => _workstationView;
    public IReadOnlyList<Farm> Farms => _farmView;
    public IReadOnlyList<SettlementConstructionSite> ConstructionSites => _constructionSiteView;
    public NeedsTuning NeedsTuning { get; }
    public ReservationService Reservations { get; }
    public CharacterStats DefaultCharacterStats { get; }
    public SpeciesCatalog? SpeciesCatalog { get; set; }
    public int? SessionSeed { get; private set; }
    public string? PlayerEntityId { get; private set; }
    public PlayerCharacterProfile? PlayerProfile { get; private set; }
    public Vector3 PlayerFacing { get; private set; } = Vector3.UnitZ;

    public SpeciesDefinition GetSpecies(string speciesId)
    {
        if (SpeciesCatalog == null) throw new InvalidOperationException("SpeciesCatalog is not configured in the world.");
        return SpeciesCatalog.GetRequired(speciesId);
    }

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

    public void SetPlayerFacing(Vector3 facing)
    {
        if (!float.IsFinite(facing.X) || !float.IsFinite(facing.Y) || !float.IsFinite(facing.Z)
            || Math.Abs(facing.Y) > 0.0001f || facing.LengthSquared() < 0.999f || facing.LengthSquared() > 1.001f)
            throw new ArgumentOutOfRangeException(nameof(facing));
        PlayerFacing = Vector3.Normalize(new Vector3(facing.X, 0, facing.Z));
    }

    public FoodSource AddFoodSource(string id, Vector3 position, ResourceContainer contents) =>
        AddFoodSource(id, position, contents, null);

    public FoodSource AddFoodSource(string id, Vector3 position, ResourceContainer contents, double? freshnessSecondsRemaining)
    {
        var source = new FoodSource(id, position, contents, freshnessSecondsRemaining);
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

    public ResourceLocation AddStockpile(string id, Vector3 position, ResourceContainer contents) =>
        AddStockpile(id, position, contents, null);

    public ResourceLocation AddStockpile(
        string id,
        Vector3 position,
        ResourceContainer contents,
        string? factionOwnerId)
    {
        if (factionOwnerId is not null && !_factionIds.Contains(factionOwnerId))
        {
            throw new ArgumentException(
                $"Stockpile faction owner '{factionOwnerId}' is not defined in this world.",
                nameof(factionOwnerId));
        }

        var stockpile = new ResourceLocation(id, position, contents);
        stockpile.FactionOwnerId = factionOwnerId;
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

        stockpile.FactionOwnerId = null;
        _stockpiles.Remove(stockpile);
        Reservations.RemoveTarget(stockpile.Id);
        return true;
    }

    public Structure AddStructure(string id, StructureType type, Vector3 position)
    {
        var structure = new Structure(id, type, position);
        AddTargetId(structure.Id, nameof(id));
        _structures.Add(structure);
        return structure;
    }

    public Workstation AddWorkstation(string id, Vector3 position, int inputCapacity = 10, int outputCapacity = 10)
    {
        var workstation = new Workstation(id, position, inputCapacity, outputCapacity);
        AddTargetId(workstation.Id, nameof(id));
        _workstations.Add(workstation);
        return workstation;
    }

    public Farm AddFarm(string id, Vector3 position)
    {
        var farm = new Farm(id, position);
        AddTargetId(farm.Id, nameof(id));
        _farms.Add(farm);
        return farm;
    }

    public SettlementConstructionSite AddConstructionSite(string id, Vector3 position, ConstructionBlueprint blueprint)
    {
        var site = new SettlementConstructionSite(id, position, blueprint);
        AddTargetId(site.Id, nameof(id));
        _constructionSites.Add(site);
        return site;
    }
    public Fireplace AddFireplace(string id, Vector3 position) { var fire = new Fireplace(id, position); _fireplaces.Add(fire); return fire; }

    public bool TryRemoveConstructionSite(string? id)
    {
        var site = _constructionSites.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
        if (site is null) return false;
        _constructionSites.Remove(site);
        Reservations.RemoveTarget(site.Id);
        return true;
    }

    public Structure? GetStructureAt(Vector3 position, double tolerance = 1.0)
    {
        return _structures.FirstOrDefault(s => Vector3.Distance(s.Position, position) <= (float)tolerance);
    }

    public FactionOwnershipResult SetStockpileFactionOwner(string? stockpileId, string? factionId)
    {
        var stockpile = FindTarget(_stockpiles, stockpileId);
        if (stockpile is null)
        {
            return FactionOwnershipResult.ContainerNotFound;
        }

        if (factionId is null)
        {
            stockpile.FactionOwnerId = null;
            return FactionOwnershipResult.Removed;
        }

        if (!_factionIds.Contains(factionId))
        {
            return FactionOwnershipResult.UnknownFaction;
        }

        stockpile.FactionOwnerId = factionId;
        return FactionOwnershipResult.Assigned;
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
        var result = TryTransitionCreatureState(entityId, CreatureCaptivityState.Captured);
        return result switch
        {
            CreatureLifecycleTransitionResult.Transitioned => CreatureCaptureResult.Captured,
            CreatureLifecycleTransitionResult.AlreadyInState => CreatureCaptureResult.AlreadyCaptured,
            CreatureLifecycleTransitionResult.TargetNotFound => CreatureCaptureResult.TargetNotFound,
            CreatureLifecycleTransitionResult.NotCreature => CreatureCaptureResult.NotCreature,
            CreatureLifecycleTransitionResult.TargetDead => CreatureCaptureResult.TargetDead,
            CreatureLifecycleTransitionResult.InvalidTransition
                when TryGetEntity(entityId, out var entity)
                    && entity?.CaptivityState == CreatureCaptivityState.Captive
                => CreatureCaptureResult.AlreadyCaptive,
            _ => CreatureCaptureResult.InvalidTransition
        };
    }

    public CreatureLifecycleTransitionResult TryTransitionCreatureState(
        string? entityId,
        CreatureCaptivityState targetState)
    {
        if (!TryGetEntity(entityId, out var entity) || entity is null)
        {
            return CreatureLifecycleTransitionResult.TargetNotFound;
        }

        if (string.Equals(entity.SpeciesId, "human", StringComparison.Ordinal))
        {
            return CreatureLifecycleTransitionResult.NotCreature;
        }

        if (entity.Health.IsDead)
        {
            return CreatureLifecycleTransitionResult.TargetDead;
        }

        if (!Enum.IsDefined(targetState))
        {
            return CreatureLifecycleTransitionResult.InvalidTransition;
        }

        if (entity.CaptivityState == targetState)
        {
            return CreatureLifecycleTransitionResult.AlreadyInState;
        }

        var isAllowed = (entity.CaptivityState, targetState) switch
        {
            (CreatureCaptivityState.Wild, CreatureCaptivityState.Captured) => true,
            (CreatureCaptivityState.Captured, CreatureCaptivityState.Captive) => true,
            (CreatureCaptivityState.Captive, CreatureCaptivityState.Domesticated) => true,
            (CreatureCaptivityState.Captured or CreatureCaptivityState.Captive
                or CreatureCaptivityState.Domesticated, CreatureCaptivityState.Wild) => true,
            _ => false
        };
        if (!isAllowed)
        {
            return CreatureLifecycleTransitionResult.InvalidTransition;
        }

        entity.CaptivityState = targetState;
        return CreatureLifecycleTransitionResult.Transitioned;
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
        if (updatedValue != currentValue) FactionStandingChanged?.Invoke(entityId, factionId, updatedValue - currentValue);
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

        relationship = CreateInitialRelationship(key.Item1, key.Item2);
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

        var before = (relationship.Familiarity, relationship.Trust, relationship.Fear, relationship.Loyalty);
        relationship.ApplyDelta(RelationshipDimension.Familiarity, adjustment.FamiliarityDelta);
        relationship.ApplyDelta(RelationshipDimension.Trust, adjustment.TrustDelta);
        relationship.ApplyDelta(RelationshipDimension.Fear, adjustment.FearDelta);
        relationship.ApplyDelta(RelationshipDimension.Loyalty, adjustment.LoyaltyDelta);
        var applied = new RelationshipAdjustment(relationship.Familiarity - before.Familiarity,
            relationship.Trust - before.Trust, relationship.Fear - before.Fear, relationship.Loyalty - before.Loyalty);
        if (applied.FamiliarityDelta != 0 || applied.TrustDelta != 0 || applied.FearDelta != 0 || applied.LoyaltyDelta != 0)
            RelationshipChanged?.Invoke(relationship.ObserverEntityId, relationship.TargetEntityId, applied);
        return RelationshipAdjustmentResult.Applied;
    }

    public RelationshipBondLevel GetBondLevel(string observerId, string targetId)
    {
        if (!TryGetEntity(observerId, out _) || !TryGetEntity(targetId, out _) || observerId == targetId)
            return RelationshipBondLevel.Stranger;
        return RelationshipBondClassifier.Classify(TryGetRelationship(observerId, targetId, out var relationship)
            ? relationship! : CreateInitialRelationship(observerId, targetId));
    }

    private RelationshipState CreateInitialRelationship(string observerId, string targetId)
    {
        var relationship = new RelationshipState(observerId, targetId);
        if (SpeciesCatalog is { } catalog && TryGetEntity(observerId, out var observer)
            && TryGetEntity(targetId, out var target) && observer is not null && target is not null
            && catalog.TryGet(observer.SpeciesId, out var species) && species is { IsDomestic: true }
            && catalog.TryGet(target.SpeciesId, out var targetSpecies) && targetSpecies is not null
            && (target.SpeciesId == "human" || !targetSpecies.IsPredator))
        {
            relationship.ApplyDelta(RelationshipDimension.Familiarity, RelationshipBondThresholds.ProvisionalDefault.FamiliarityForFamiliar);
            relationship.ApplyDelta(RelationshipDimension.Trust, species.BaseTrust);
        }
        return relationship;
    }

    public bool TryRemoveEntity(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !_entities.TryGetValue(id, out var entity))
        {
            return false;
        }

        entity.Actions.Cancel();
        foreach (var structure in _structures)
        foreach (var bed in structure.Beds)
            if (bed.OccupantId == id) { bed.OccupantId = null; bed.IsOccupied = false; }
        Reservations.ReleaseForEntity(id);
        _squadMembers.Remove(id);
        _workSchedules.Remove(id);
        _homePositions.Remove(id);
        _wildTerritories.Remove(id);
        foreach (var pair in _nearPopulations.ToArray())
        {
            if (!pair.Value.EntityIds.Contains(id, StringComparer.Ordinal)) continue;
            var remaining = pair.Value.EntityIds.Where(entityId => entityId != id).ToArray();
            _nearPopulations[pair.Key] = new NearPopulationRecord(pair.Key.RegionId, pair.Key.SpeciesId,
                remaining, pair.Value.Resources);
        }
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

    /// <summary>Moves an actor only when its destination preserves spacing from live actors.</summary>
    public bool TryMoveEntityPosition(string? id, Vector3 position)
    {
        if (!TryGetEntity(id, out var entity) || entity is null) return false;
        if (!ActorSpacing.IsPositionAvailable(this, entity, position)) return false;
        entity.Position = position;
        return true;
    }

    public void Reset()
    {
        foreach (var stockpile in _stockpiles)
        {
            stockpile.FactionOwnerId = null;
        }

        foreach (var entity in _entities.Values)
        {
            entity.Actions.Cancel();
            Reservations.ReleaseForEntity(entity.Id);
            entity.AllegianceFactionId = null;
        }

        _entities.Clear();
        _squadMembers.Clear();
        _workSchedules.Clear();
        _homePositions.Clear();
        _wildTerritories.Clear();
        _farPopulations.Clear();
        _nearPopulations.Clear();
        _relationships.Clear();
        _factionIds.Clear();
        _factionReputations.Clear();
        _factionHostilities.Clear();
        _foodSources.Clear();
        _resourcePiles.Clear();
        _stockpiles.Clear();
        _structures.Clear();
        _workstations.Clear();
        _farms.Clear();
        _constructionSites.Clear();
        _fireplaces.Clear();
        _treeRegrowthTimers.Clear();
        _targetIds.Clear();
        Reservations.Reset();
        SessionSeed = null;
        PlayerEntityId = null;
        PlayerProfile = null;
        PlayerFacing = Vector3.UnitZ;
    }

    internal void ApplySnapshot(WorldSnapshot snapshot, SpeciesCatalog speciesCatalog)
    {
        SpeciesCatalog = speciesCatalog;
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
            restored.Parent1Id = entity.Parent1Id;
            restored.Parent2Id = entity.Parent2Id;
            restored.SetSleepState(entity.SleepState, entity.BedId);
            restored.EquippedTool = entity.EquippedTool;
            foreach (var item in entity.Inventory)
            {
                restored.TryAddCarried(item.Key, item.Value);
            }
            if (entity.Id == snapshot.PlayerEntityId && snapshot.PlayerHealth is not null)
                restored.Health.Restore(snapshot.PlayerHealth, snapshot.PlayerPendingDeathSeconds, snapshot.PlayerIsDead);
            if (entity.IsDead && !restored.Health.IsDead)
                restored.Health.RestoreDeadState();
        }
        foreach (var timer in snapshot.TreeRegrowthTimers ?? Array.Empty<TreeRegrowthSnapshot>())
            _treeRegrowthTimers[timer.TreeId] = timer.ElapsedSeconds;
        foreach (var fire in snapshot.Fireplaces ?? Array.Empty<FireplaceSnapshot>())
            AddFireplace(fire.Id, fire.Position).Fuel = fire.Fuel;

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

            var restored = AddFoodSource(source.Id, source.Position, contents);
            restored.RestoreFreshness(source.FreshnessSecondsRemaining);
        }

        foreach (var pile in snapshot.ResourcePiles)
        {
            AddResourcePile(pile.Id, pile.Position, RestoreContainer(pile.Contents));
        }

        foreach (var stockpile in snapshot.Stockpiles)
        {
            AddStockpile(
                stockpile.Id,
                stockpile.Position,
                RestoreContainer(stockpile.Contents),
                stockpile.FactionOwnerId);
        }

        foreach (var saved in snapshot.Workstations!)
        {
            var workstation = AddWorkstation(saved.Id, saved.Position,
                saved.InputStorage.Capacity, saved.OutputStorage.Capacity);
            foreach (var item in saved.InputStorage.Counts) workstation.InputStorage.AddKnown(item.Key, item.Value);
            foreach (var item in saved.OutputStorage.Counts) workstation.OutputStorage.AddKnown(item.Key, item.Value);
            if (saved.CurrentRecipe is { } savedRecipe)
            {
                var recipe = new WorkstationRecipe(savedRecipe.Id, savedRecipe.Inputs, savedRecipe.Outputs, savedRecipe.WorkRequired);
                recipe.RestoreProgress(savedRecipe.WorkProgress);
                workstation.SetRecipe(recipe);
            }
        }

        foreach (var saved in snapshot.Farms!)
        {
            var farm = AddFarm(saved.Id, saved.Position);
            foreach (var savedPlot in saved.Plots)
            {
                var plot = new FarmPlot(savedPlot.Id, savedPlot.Capacity);
                plot.Restore(savedPlot.CropId is null ? null : SettlementCrops.GetRequired(savedPlot.CropId), savedPlot.Growth);
                farm.AddPlot(plot);
            }
        }

        foreach (var saved in snapshot.Structures!)
        {
            var structure = AddStructure(saved.Id, saved.Type, saved.Position);
            structure.IsClosed = saved.IsClosed;
            foreach (var bed in saved.Beds ?? Array.Empty<BedSnapshot>())
                structure.AddBed(new Bed(bed.Id, bed.RecoveryRateMultiplier) { OccupantId = bed.OccupantId, IsOccupied = bed.OccupantId is not null });
        }

        foreach (var saved in snapshot.ConstructionSites!)
        {
            var blueprint = new ConstructionBlueprint(saved.BlueprintId, saved.Width, saved.Depth,
                saved.RequiredMaterials, saved.WorkRequired);
            blueprint.Restore(saved.MaterialProgress, saved.WorkProgress);
            AddConstructionSite(saved.Id, saved.Position, blueprint);
        }

        foreach (var savedRelationship in snapshot.Relationships)
        {
            // Saved totals are authoritative, including explicit zero/betrayed bonds.
            var relationship = new RelationshipState(savedRelationship.ObserverEntityId, savedRelationship.TargetEntityId);
            _relationships.Add((savedRelationship.ObserverEntityId, savedRelationship.TargetEntityId), relationship);

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
            SetPlayerFacing(snapshot.PlayerFacing);
        }
        foreach (var id in snapshot.SquadMemberIds!) _squadMembers.Add(id);
        foreach (var setting in snapshot.WorkSchedules!)
            _workSchedules.Add(setting.EntityId, new(setting.StartHour, setting.EndHour));
        foreach (var home in snapshot.HomePositions!)
            _homePositions.Add(home.EntityId, home.Position);
        foreach (var territory in snapshot.WildTerritories!)
            _wildTerritories.Add(territory.EntityId,
                new WildTerritory(territory.EntityId, territory.Center, territory.Radius));

        foreach (var population in snapshot.FarPopulations!)
            SetFarPopulation(new FarEntityRecord(
                population.RegionId,
                population.SpeciesId,
                population.Count,
                population.AverageSatiety,
                population.Resources));
        foreach (var population in snapshot.NearPopulations!)
            SetNearPopulation(new NearPopulationRecord(population.RegionId, population.SpeciesId,
                population.EntityIds, population.Resources));
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
