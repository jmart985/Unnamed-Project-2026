using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Simulation;

public sealed record WorldSnapshot(
    int SchemaVersion,
    double SimulationTime,
    long LastAllocatedEntityId,
    IReadOnlyList<EntitySnapshot> Entities,
    IReadOnlyList<FoodSourceSnapshot> FoodSources,
    IReadOnlyList<ResourceLocationSnapshot> ResourcePiles,
    IReadOnlyList<ResourceLocationSnapshot> Stockpiles,
    IReadOnlyList<RelationshipSnapshot> Relationships,
    int? SessionSeed,
    string? PlayerEntityId,
    PlayerCharacterProfile? PlayerProfile,
    IReadOnlyList<string>? FactionIds = null,
    IReadOnlyList<FactionReputationSnapshot>? FactionReputations = null,
    IReadOnlyList<FactionHostilitySnapshot>? FactionHostilities = null,
    IReadOnlyList<string>? SquadMemberIds = null,
    IReadOnlyList<NpcScheduleSetting>? WorkSchedules = null,
    IReadOnlyList<NpcHomeSetting>? HomePositions = null,
    IReadOnlyList<WorkstationSnapshot>? Workstations = null,
    IReadOnlyList<FarmSnapshot>? Farms = null,
    IReadOnlyList<StructureSnapshot>? Structures = null,
    IReadOnlyList<ConstructionSiteSnapshot>? ConstructionSites = null);

public sealed record StructureSnapshot(string Id, StructureType Type, Vector3 Position, bool IsClosed, IReadOnlyList<BedSnapshot>? Beds = null);
public sealed record BedSnapshot(string Id, double RecoveryRateMultiplier, string? OccupantId);

public sealed record ConstructionSiteSnapshot(
    string Id,
    Vector3 Position,
    string BlueprintId,
    int Width,
    int Depth,
    IReadOnlyDictionary<string, int> RequiredMaterials,
    IReadOnlyDictionary<string, int> MaterialProgress,
    int WorkRequired,
    int WorkProgress);

public sealed record FarmSnapshot(
    string Id,
    Vector3 Position,
    IReadOnlyList<FarmPlotSnapshot> Plots);

public sealed record FarmPlotSnapshot(
    string Id,
    int Capacity,
    string? CropId,
    int Growth);

public sealed record WorkstationSnapshot(
    string Id,
    Vector3 Position,
    ResourceContainerSnapshot InputStorage,
    ResourceContainerSnapshot OutputStorage,
    WorkstationRecipeSnapshot? CurrentRecipe);

public sealed record WorkstationRecipeSnapshot(
    string Id,
    IReadOnlyDictionary<string, int> Inputs,
    IReadOnlyDictionary<string, int> Outputs,
    int WorkRequired,
    int WorkProgress);

public sealed record RelationshipSnapshot(
    string ObserverEntityId,
    string TargetEntityId,
    double Familiarity,
    double Trust,
    double Fear,
    double Loyalty);

public sealed record FactionReputationSnapshot(
    string EntityId,
    string FactionId,
    double Value);

public sealed record FactionHostilitySnapshot(
    string SourceFactionId,
    string TargetFactionId,
    bool IsHostile);

public sealed record EntitySnapshot(
    string Id,
    string SpeciesId,
    Vector3 Position,
    double Satiety,
    IReadOnlyDictionary<string, int> Inventory,
    CreatureCaptivityState CaptivityState = CreatureCaptivityState.Wild,
    string? AllegianceFactionId = null,
    string? Parent1Id = null,
    string? Parent2Id = null,
    SleepState SleepState = SleepState.Awake,
    string? BedId = null);

public sealed record FoodSourceSnapshot(
    string Id,
    Vector3 Position,
    ResourceContainerSnapshot Contents);

public sealed record ResourceLocationSnapshot(
    string Id,
    Vector3 Position,
    ResourceContainerSnapshot Contents,
    string? FactionOwnerId = null);

public interface ITransientSimulationState
{
    void Reset();
}

public sealed record ResourceContainerSnapshot(
    string Name,
    int Capacity,
    IReadOnlyDictionary<string, int> Counts);

public static class WorldSnapshotSerializer
{
    private const int LegacySchemaVersion = 4;
    private const int PreviousSchemaVersion = 5;
    private const int FactionIdentitySchemaVersion = 6;
    private const int FactionRelationsSchemaVersion = 7;
    private const int OwnershipSchemaVersion = 8;
    private const int SquadSchemaVersion = 9;
    private const int ScheduleSchemaVersion = 10;
    private const int WorkstationSchemaVersion = 11;
    private const int FarmSchemaVersion = 12;
    private const int ConstructionSchemaVersion = 13;
    private const int BedSchemaVersion = 14;
    public const int CurrentSchemaVersion = 15;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static WorldSnapshot Capture(World world, double simulationTime)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!double.IsFinite(simulationTime) || simulationTime < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(simulationTime), "Simulation time must be finite and nonnegative.");
        }

        var entities = world.Entities
            .OrderBy(entity => entity.Id, StringComparer.Ordinal)
            .Select(entity => new EntitySnapshot(
                entity.Id,
                entity.SpeciesId,
                entity.Position,
                entity.Satiety,
                new Dictionary<string, int>(entity.Inventory, StringComparer.Ordinal),
                entity.CaptivityState,
                entity.AllegianceFactionId,
                entity.Parent1Id,
                entity.Parent2Id,
                entity.SleepState,
                entity.BedId))
            .ToArray();
        var foodSources = world.FoodSources
            .Select(source => new FoodSourceSnapshot(
                source.Id,
                source.Position,
                CaptureContainer(source.Contents)))
            .ToArray();
        var resourcePiles = world.ResourcePiles.Select(CaptureLocation).ToArray();
        var stockpiles = world.Stockpiles.Select(CaptureLocation).ToArray();
        var relationships = world.Relationships
            .OrderBy(relationship => relationship.ObserverEntityId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.TargetEntityId, StringComparer.Ordinal)
            .Select(relationship => new RelationshipSnapshot(
                relationship.ObserverEntityId,
                relationship.TargetEntityId,
                relationship.Familiarity,
                relationship.Trust,
                relationship.Fear,
                relationship.Loyalty))
            .ToArray();
        var factionReputations = world.FactionReputations
            .Select(reputation => new FactionReputationSnapshot(
                reputation.EntityId,
                reputation.FactionId,
                reputation.Value))
            .ToArray();
        var factionHostilities = world.FactionHostilities
            .Select(hostility => new FactionHostilitySnapshot(
                hostility.SourceFactionId,
                hostility.TargetFactionId,
                hostility.IsHostile))
            .ToArray();

        return new WorldSnapshot(
            CurrentSchemaVersion,
            simulationTime,
            world.LastAllocatedEntityId,
            entities,
            foodSources,
            resourcePiles,
            stockpiles,
            relationships,
            world.SessionSeed,
            world.PlayerEntityId,
            world.PlayerProfile,
            world.FactionIds.ToArray(),
            factionReputations,
            factionHostilities,
            world.SquadMemberIds.ToArray(),
            world.WorkSchedules.ToArray(),
            world.HomePositions.ToArray(),
            world.Workstations.OrderBy(item => item.Id, StringComparer.Ordinal).Select(item =>
                new WorkstationSnapshot(item.Id, item.Position, CaptureContainer(item.InputStorage),
                    CaptureContainer(item.OutputStorage), item.CurrentRecipe is null ? null :
                        new WorkstationRecipeSnapshot(item.CurrentRecipe.Id,
                            new Dictionary<string, int>(item.CurrentRecipe.Inputs, StringComparer.Ordinal),
                            new Dictionary<string, int>(item.CurrentRecipe.Outputs, StringComparer.Ordinal),
                            item.CurrentRecipe.WorkRequired, item.CurrentRecipe.WorkProgress))).ToArray(),
            world.Farms.OrderBy(item => item.Id, StringComparer.Ordinal).Select(farm =>
                new FarmSnapshot(farm.Id, farm.Position, farm.Plots.Select(plot =>
                    new FarmPlotSnapshot(plot.Id, plot.Capacity, plot.Crop?.Id, plot.Growth)).ToArray())).ToArray(),
            world.Structures.OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => new StructureSnapshot(item.Id, item.Type, item.Position, item.IsClosed,
                    item.Beds.Select(bed => new BedSnapshot(bed.Id, bed.RecoveryRateMultiplier, bed.OccupantId)).ToArray())).ToArray(),
            world.ConstructionSites.OrderBy(item => item.Id, StringComparer.Ordinal).Select(site =>
                new ConstructionSiteSnapshot(site.Id, site.Position, site.Blueprint.Id, site.Blueprint.Width, site.Blueprint.Depth,
                    new Dictionary<string, int>(site.Blueprint.RequiredMaterials, StringComparer.Ordinal),
                    new Dictionary<string, int>(site.Blueprint.Progress, StringComparer.Ordinal),
                    site.Blueprint.WorkRequired, site.Blueprint.WorkProgress)).ToArray());
    }

    public static string Serialize(World world, double simulationTime)
    {
        return JsonSerializer.Serialize(Capture(world, simulationTime), JsonOptions);
    }

    public static WorldSnapshot Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Snapshot JSON is required.", nameof(json));
        }

        WorldSnapshot snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<WorldSnapshot>(json, JsonOptions)
                ?? throw new InvalidDataException("Snapshot JSON did not contain a snapshot.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Snapshot JSON is malformed.", exception);
        }

        if (snapshot.SchemaVersion is LegacySchemaVersion
            or PreviousSchemaVersion
            or FactionIdentitySchemaVersion
            or FactionRelationsSchemaVersion)
        {
            return snapshot with
            {
                SchemaVersion = CurrentSchemaVersion,
                SquadMemberIds = Array.Empty<string>(),
                WorkSchedules = Array.Empty<NpcScheduleSetting>(),
                HomePositions = Array.Empty<NpcHomeSetting>(),
                Workstations = LegacyWorkstations(snapshot),
                Farms = LegacyFarms(snapshot),
                Structures = Array.Empty<StructureSnapshot>(),
                ConstructionSites = Array.Empty<ConstructionSiteSnapshot>(),
                FactionIds = snapshot.FactionIds ?? Array.Empty<string>(),
                FactionReputations = snapshot.SchemaVersion == FactionRelationsSchemaVersion
                    ? snapshot.FactionReputations
                    : Array.Empty<FactionReputationSnapshot>(),
                FactionHostilities = snapshot.SchemaVersion == FactionRelationsSchemaVersion
                    ? snapshot.FactionHostilities
                    : Array.Empty<FactionHostilitySnapshot>(),
                Stockpiles = snapshot.Stockpiles?.Select(stockpile =>
                    stockpile is null ? null! : stockpile with { FactionOwnerId = null }).ToArray()!
            };
        }

        if (snapshot.SchemaVersion == OwnershipSchemaVersion)
            return snapshot with { SchemaVersion = CurrentSchemaVersion, SquadMemberIds = Array.Empty<string>(), WorkSchedules = Array.Empty<NpcScheduleSetting>(), HomePositions = Array.Empty<NpcHomeSetting>(), Workstations = LegacyWorkstations(snapshot), Farms = LegacyFarms(snapshot), Structures = Array.Empty<StructureSnapshot>(), ConstructionSites = Array.Empty<ConstructionSiteSnapshot>() };

        if (snapshot.SchemaVersion == SquadSchemaVersion)
            return snapshot with { SchemaVersion = CurrentSchemaVersion, WorkSchedules = Array.Empty<NpcScheduleSetting>(), HomePositions = Array.Empty<NpcHomeSetting>(), Workstations = LegacyWorkstations(snapshot), Farms = LegacyFarms(snapshot), Structures = Array.Empty<StructureSnapshot>(), ConstructionSites = Array.Empty<ConstructionSiteSnapshot>() };

        if (snapshot.SchemaVersion == ScheduleSchemaVersion)
            return snapshot with { SchemaVersion = CurrentSchemaVersion, HomePositions = Array.Empty<NpcHomeSetting>(), Workstations = LegacyWorkstations(snapshot), Farms = LegacyFarms(snapshot), Structures = Array.Empty<StructureSnapshot>(), ConstructionSites = Array.Empty<ConstructionSiteSnapshot>() };

        if (snapshot.SchemaVersion == WorkstationSchemaVersion)
            return snapshot with { SchemaVersion = CurrentSchemaVersion, HomePositions = Array.Empty<NpcHomeSetting>(), Farms = LegacyFarms(snapshot), Structures = Array.Empty<StructureSnapshot>(), ConstructionSites = Array.Empty<ConstructionSiteSnapshot>() };

        if (snapshot.SchemaVersion == FarmSchemaVersion)
            return snapshot with { SchemaVersion = CurrentSchemaVersion, HomePositions = Array.Empty<NpcHomeSetting>(), Structures = Array.Empty<StructureSnapshot>(), ConstructionSites = Array.Empty<ConstructionSiteSnapshot>() };

        if (snapshot.SchemaVersion == ConstructionSchemaVersion)
            return snapshot with { SchemaVersion = CurrentSchemaVersion, HomePositions = Array.Empty<NpcHomeSetting>(), Structures = snapshot.Structures?.Select(structure => structure with
            {
                Beds = structure.Beds is { Count: > 0 } ? structure.Beds : structure.Type == StructureType.Hut
                    ? new[] { new BedSnapshot($"{structure.Id}-bed-1", 1.0, null) } : Array.Empty<BedSnapshot>()
            }).ToArray() ?? Array.Empty<StructureSnapshot>() };

        if (snapshot.SchemaVersion == BedSchemaVersion)
            return snapshot with { SchemaVersion = CurrentSchemaVersion, HomePositions = Array.Empty<NpcHomeSetting>() };

        if (snapshot.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported world snapshot schema version {snapshot.SchemaVersion}.");
        }

        return snapshot;
    }

    private static IReadOnlyList<WorkstationSnapshot> LegacyWorkstations(WorldSnapshot snapshot)
    {
        if (snapshot.SessionSeed is null) return Array.Empty<WorkstationSnapshot>();
        var recipe = SettlementRecipes.CreateBasicMeal();
        return [new WorkstationSnapshot("workbench-1", NewGameSession.TownWorkbenchPosition,
            new ResourceContainerSnapshot("workbench-1-input", 10, new Dictionary<string, int>()),
            new ResourceContainerSnapshot("workbench-1-output", 10, new Dictionary<string, int>()),
            new WorkstationRecipeSnapshot(recipe.Id, recipe.Inputs, recipe.Outputs, recipe.WorkRequired, 0))];
    }

    private static IReadOnlyList<FarmSnapshot> LegacyFarms(WorldSnapshot snapshot) => snapshot.SessionSeed is null
        ? Array.Empty<FarmSnapshot>()
        : [new FarmSnapshot("farm-1", new Vector3(5, 0, 5),
            [new FarmPlotSnapshot("farm-1-plot-1", 1, null, 0)])];

    public static double Load(
        World target,
        string json,
        SpeciesCatalog speciesCatalog,
        params ITransientSimulationState[] transientState)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(speciesCatalog);

        var snapshot = Deserialize(json);
        Validate(snapshot, target, speciesCatalog);

        foreach (var state in transientState)
        {
            ArgumentNullException.ThrowIfNull(state);
        }

        foreach (var state in transientState)
        {
            state.Reset();
        }

        target.ApplySnapshot(snapshot, speciesCatalog);
        return snapshot.SimulationTime;
    }

    private static void Validate(
        WorldSnapshot snapshot,
        World target,
        SpeciesCatalog speciesCatalog)
    {
        if (!double.IsFinite(snapshot.SimulationTime) || snapshot.SimulationTime < 0)
        {
            throw new InvalidDataException("Snapshot simulation time must be finite and nonnegative.");
        }

        if (snapshot.LastAllocatedEntityId < 0)
        {
            throw new InvalidDataException("Snapshot last allocated entity ID cannot be negative.");
        }

        if (snapshot.Entities is null || snapshot.FoodSources is null
            || snapshot.ResourcePiles is null || snapshot.Stockpiles is null
            || snapshot.Relationships is null)
        {
            throw new InvalidDataException("Snapshot entity, resource, and relationship lists are required.");
        }

        if (snapshot.FactionIds is null)
        {
            throw new InvalidDataException("Snapshot faction IDs are required.");
        }

        if (snapshot.FactionReputations is null || snapshot.FactionHostilities is null)
        {
            throw new InvalidDataException("Snapshot faction reputation and hostility lists are required.");
        }

        var factionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var factionId in snapshot.FactionIds)
        {
            if (!World.IsValidFactionId(factionId))
            {
                throw new InvalidDataException("Snapshot faction IDs must be valid lowercase faction IDs.");
            }

            if (!factionIds.Add(factionId))
            {
                throw new InvalidDataException($"Snapshot contains duplicate faction ID '{factionId}'.");
            }
        }

        var entityIds = new HashSet<string>(StringComparer.Ordinal);
        var highestEntityId = 0L;
        foreach (var entity in snapshot.Entities)
        {
            if (entity is null || string.IsNullOrWhiteSpace(entity.Id))
            {
                throw new InvalidDataException("Snapshot entities must have IDs.");
            }

            if (!entityIds.Add(entity.Id))
            {
                throw new InvalidDataException($"Snapshot contains duplicate entity ID '{entity.Id}'.");
            }

            if (!speciesCatalog.TryGet(entity.SpeciesId, out _))
            {
                throw new InvalidDataException(
                    $"Snapshot entity '{entity.Id}' references unknown species '{entity.SpeciesId}'.");
            }

            ValidatePosition(entity.Position, $"entity '{entity.Id}' position");
            if (!double.IsFinite(entity.Satiety) || entity.Satiety is < 0 or > 100)
            {
                throw new InvalidDataException(
                    $"Snapshot entity '{entity.Id}' satiety must be finite and within [0, 100].");
            }

            if (!Enum.IsDefined(entity.CaptivityState)
                || (entity.CaptivityState != CreatureCaptivityState.Wild
                    && string.Equals(entity.SpeciesId, "human", StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    $"Snapshot entity '{entity.Id}' has an invalid creature captivity state.");
            }

            if (entity.AllegianceFactionId is { } factionId && !factionIds.Contains(factionId))
            {
                throw new InvalidDataException(
                    $"Snapshot entity '{entity.Id}' references unknown faction '{factionId}'.");
            }

            ValidateInventory(entity.Inventory, target.InventoryCapacity, entity.Id);
            highestEntityId = Math.Max(highestEntityId, ParseEntityId(entity.Id));
        }
        if (snapshot.LastAllocatedEntityId < highestEntityId)
        {
            throw new InvalidDataException(
                "Snapshot last allocated entity ID must include every entity ID.");
        }

        ValidateSessionPlayer(snapshot, entityIds);
        if (snapshot.SquadMemberIds is null) throw new InvalidDataException("Snapshot squad membership is required.");
        var memberIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in snapshot.SquadMemberIds)
        {
            if (string.IsNullOrWhiteSpace(id) || !memberIds.Add(id)
                || id == snapshot.PlayerEntityId
                || !snapshot.Entities.Any(entity => entity.Id == id && entity.SpeciesId == "human"))
                throw new InvalidDataException("Snapshot squad members must be distinct live human NPCs.");
        }
        if (snapshot.WorkSchedules is null) throw new InvalidDataException("Snapshot work schedules are required.");
        var scheduleIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var setting in snapshot.WorkSchedules)
        {
            if (setting is null || string.IsNullOrWhiteSpace(setting.EntityId)
                || !scheduleIds.Add(setting.EntityId) || setting.EntityId == snapshot.PlayerEntityId
                || !snapshot.Entities.Any(entity => entity.Id == setting.EntityId && entity.SpeciesId == "human")
                || !new NpcWorkSchedule(setting.StartHour, setting.EndHour).IsValid)
                throw new InvalidDataException("Snapshot work schedules must reference distinct human NPCs with valid work hours.");
        }
        if (snapshot.HomePositions is null) throw new InvalidDataException("Snapshot home positions are required.");
        var homeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var home in snapshot.HomePositions)
        {
            if (home is null || string.IsNullOrWhiteSpace(home.EntityId)
                || !homeIds.Add(home.EntityId) || home.EntityId == snapshot.PlayerEntityId
                || !snapshot.Entities.Any(entity => entity.Id == home.EntityId && entity.SpeciesId == "human"))
                throw new InvalidDataException("Snapshot home positions must reference distinct human NPCs.");
            ValidatePosition(home.Position, $"home '{home.EntityId}' position");
        }
        if (snapshot.Workstations is null) throw new InvalidDataException("Snapshot workstations are required.");
        if (snapshot.Farms is null) throw new InvalidDataException("Snapshot farms are required.");
        if (snapshot.Structures is null || snapshot.ConstructionSites is null)
            throw new InvalidDataException("Snapshot construction state is required.");
        var foodSourceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in snapshot.FoodSources)
        {
            if (source is null || string.IsNullOrWhiteSpace(source.Id))
            {
                throw new InvalidDataException("Snapshot food sources must have IDs.");
            }

            if (!foodSourceIds.Add(source.Id))
            {
                throw new InvalidDataException($"Snapshot contains duplicate food source ID '{source.Id}'.");
            }

            ValidatePosition(source.Position, $"food source '{source.Id}' position");
            ValidateContainer(source.Contents, source.Id);
        }

        ValidateLocations(snapshot.ResourcePiles, "resource pile", foodSourceIds, factionIds, allowFactionOwner: false);
        ValidateLocations(snapshot.Stockpiles, "stockpile", foodSourceIds, factionIds, allowFactionOwner: true);
        foreach (var workstation in snapshot.Workstations)
        {
            if (workstation is null || string.IsNullOrWhiteSpace(workstation.Id) || !foodSourceIds.Add(workstation.Id))
                throw new InvalidDataException("Snapshot contains an invalid or duplicate workstation ID.");
            ValidatePosition(workstation.Position, $"workstation '{workstation.Id}' position");
            ValidateContainer(workstation.InputStorage, workstation.Id);
            ValidateContainer(workstation.OutputStorage, workstation.Id);
            if (workstation.CurrentRecipe is { } recipe)
            {
                if (string.IsNullOrWhiteSpace(recipe.Id) || recipe.Inputs is null || recipe.Outputs is null
                    || recipe.WorkRequired <= 0 || recipe.WorkProgress < 0 || recipe.WorkProgress > recipe.WorkRequired
                    || recipe.Outputs.Count == 0)
                    throw new InvalidDataException($"Snapshot workstation '{workstation.Id}' has an invalid recipe.");
                foreach (var item in recipe.Inputs) ValidatePositiveItemCount(item, workstation.Id);
                foreach (var item in recipe.Outputs) ValidatePositiveItemCount(item, workstation.Id);
            }
        }
        foreach (var farm in snapshot.Farms)
        {
            if (farm is null || string.IsNullOrWhiteSpace(farm.Id) || !foodSourceIds.Add(farm.Id)
                || farm.Plots is null || farm.Plots.Count == 0)
                throw new InvalidDataException("Snapshot contains an invalid or duplicate farm.");
            ValidatePosition(farm.Position, $"farm '{farm.Id}' position");
            var plotIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var plot in farm.Plots)
            {
                if (plot is null || string.IsNullOrWhiteSpace(plot.Id) || !plotIds.Add(plot.Id) || plot.Capacity <= 0)
                    throw new InvalidDataException($"Snapshot farm '{farm.Id}' contains an invalid or duplicate plot.");
                if (plot.CropId is null)
                {
                    if (plot.Growth != 0) throw new InvalidDataException($"Snapshot empty plot '{plot.Id}' cannot have growth.");
                }
                else
                {
                    CropDefinition crop;
                    try { crop = SettlementCrops.GetRequired(plot.CropId); }
                    catch (ArgumentException exception) { throw new InvalidDataException($"Snapshot plot '{plot.Id}' has an unknown crop.", exception); }
                    if (plot.Growth < 0 || plot.Growth > crop.GrowthRequired)
                        throw new InvalidDataException($"Snapshot plot '{plot.Id}' has invalid growth.");
                }
            }
        }
        foreach (var structure in snapshot.Structures)
        {
            if (structure is null || string.IsNullOrWhiteSpace(structure.Id) || !foodSourceIds.Add(structure.Id))
                throw new InvalidDataException("Snapshot contains an invalid or duplicate structure.");
            ValidatePosition(structure.Position, $"structure '{structure.Id}' position");
            var bedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var bed in structure.Beds ?? Array.Empty<BedSnapshot>())
            {
                if (bed is null || string.IsNullOrWhiteSpace(bed.Id) || !bedIds.Add(bed.Id)
                    || bed.RecoveryRateMultiplier <= 0 || !double.IsFinite(bed.RecoveryRateMultiplier)
                    || (bed.OccupantId is not null && !entityIds.Contains(bed.OccupantId)))
                    throw new InvalidDataException($"Structure '{structure.Id}' contains an invalid bed.");
            }
        }
        var occupiedBeds = snapshot.Structures.SelectMany(structure => structure.Beds ?? Array.Empty<BedSnapshot>())
            .Where(bed => bed.OccupantId is not null).ToArray();
        if (occupiedBeds.GroupBy(bed => bed.OccupantId!, StringComparer.Ordinal).Any(group => group.Count() != 1))
            throw new InvalidDataException("Snapshot bed occupancy must reference each sleeper at most once.");
        foreach (var entity in snapshot.Entities)
        {
            if (entity.SleepState == SleepState.Sleeping && (entity.BedId is null || !occupiedBeds.Any(bed => bed.Id == entity.BedId && bed.OccupantId == entity.Id)))
                throw new InvalidDataException($"Sleeping entity '{entity.Id}' must reference its occupied bed.");
            if (entity.SleepState != SleepState.Sleeping && entity.BedId is not null)
                throw new InvalidDataException($"Awake entity '{entity.Id}' cannot retain a bed assignment.");
        }
        foreach (var bed in occupiedBeds)
        {
            var occupant = snapshot.Entities.Single(entity => entity.Id == bed.OccupantId);
            if (occupant.SleepState != SleepState.Sleeping || occupant.BedId != bed.Id)
                throw new InvalidDataException($"Occupied bed '{bed.Id}' must belong to its sleeping occupant.");
        }
        foreach (var site in snapshot.ConstructionSites)
        {
            if (site is null || string.IsNullOrWhiteSpace(site.Id) || !foodSourceIds.Add(site.Id)
                || string.IsNullOrWhiteSpace(site.BlueprintId) || site.Width <= 0 || site.Depth <= 0
                || site.RequiredMaterials is null || site.MaterialProgress is null || site.RequiredMaterials.Count == 0
                || site.WorkRequired < 0 || site.WorkProgress < 0 || site.WorkProgress > site.WorkRequired)
                throw new InvalidDataException("Snapshot contains an invalid or duplicate construction site.");
            ValidatePosition(site.Position, $"construction site '{site.Id}' position");
            foreach (var requirement in site.RequiredMaterials)
                if (!ItemIds.IsKnown(requirement.Key) || requirement.Value <= 0
                    || site.MaterialProgress.GetValueOrDefault(requirement.Key) < 0
                    || site.MaterialProgress.GetValueOrDefault(requirement.Key) > requirement.Value)
                    throw new InvalidDataException($"Construction site '{site.Id}' has invalid material progress.");
            if (site.MaterialProgress.Keys.Any(key => !site.RequiredMaterials.ContainsKey(key)))
                throw new InvalidDataException($"Construction site '{site.Id}' has unknown material progress.");
            if (site.WorkProgress > 0 && site.RequiredMaterials.Any(item => site.MaterialProgress.GetValueOrDefault(item.Key) < item.Value))
                throw new InvalidDataException($"Construction site '{site.Id}' has work before materials are complete.");
        }
        ValidateRelationships(snapshot.Relationships, entityIds);
        ValidateFactionReputations(snapshot.FactionReputations, entityIds, factionIds);
        ValidateFactionHostilities(snapshot.FactionHostilities, factionIds);
    }

    private static void ValidatePositiveItemCount(KeyValuePair<string, int> item, string workstationId)
    {
        if (!ItemIds.IsKnown(item.Key) || item.Value <= 0)
            throw new InvalidDataException($"Snapshot workstation '{workstationId}' recipe has invalid materials.");
    }

    private static void ValidateFactionReputations(
        IReadOnlyList<FactionReputationSnapshot> reputations,
        HashSet<string> entityIds,
        HashSet<string> factionIds)
    {
        var reputationKeys = new HashSet<(string EntityId, string FactionId)>();
        foreach (var reputation in reputations)
        {
            if (reputation is null
                || string.IsNullOrWhiteSpace(reputation.EntityId)
                || !entityIds.Contains(reputation.EntityId)
                || string.IsNullOrWhiteSpace(reputation.FactionId)
                || !factionIds.Contains(reputation.FactionId))
            {
                throw new InvalidDataException("Snapshot faction reputation references an unknown entity or faction.");
            }

            if (!double.IsFinite(reputation.Value) || reputation.Value is < 0 or > 100)
            {
                throw new InvalidDataException(
                    $"Snapshot faction reputation for entity '{reputation.EntityId}' and faction '{reputation.FactionId}' must be finite and within [0, 100].");
            }

            if (!reputationKeys.Add((reputation.EntityId, reputation.FactionId)))
            {
                throw new InvalidDataException(
                    $"Snapshot contains duplicate faction reputation for entity '{reputation.EntityId}' and faction '{reputation.FactionId}'.");
            }
        }
    }

    private static void ValidateFactionHostilities(
        IReadOnlyList<FactionHostilitySnapshot> hostilities,
        HashSet<string> factionIds)
    {
        var hostilityKeys = new HashSet<(string SourceFactionId, string TargetFactionId)>();
        foreach (var hostility in hostilities)
        {
            if (hostility is null
                || string.IsNullOrWhiteSpace(hostility.SourceFactionId)
                || !factionIds.Contains(hostility.SourceFactionId)
                || string.IsNullOrWhiteSpace(hostility.TargetFactionId)
                || !factionIds.Contains(hostility.TargetFactionId))
            {
                throw new InvalidDataException("Snapshot faction hostility references an unknown faction.");
            }

            if (string.Equals(hostility.SourceFactionId, hostility.TargetFactionId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Snapshot faction hostility cannot target the same faction.");
            }

            if (!hostilityKeys.Add((hostility.SourceFactionId, hostility.TargetFactionId)))
            {
                throw new InvalidDataException(
                    $"Snapshot contains duplicate faction hostility from '{hostility.SourceFactionId}' to '{hostility.TargetFactionId}'.");
            }
        }
    }

    private static void ValidateSessionPlayer(
        WorldSnapshot snapshot,
        HashSet<string> entityIds)
    {
        var hasSessionMetadata = snapshot.SessionSeed is not null
            || snapshot.PlayerEntityId is not null
            || snapshot.PlayerProfile is not null;
        if (!hasSessionMetadata)
        {
            return;
        }

        if (snapshot.SessionSeed is < 0)
        {
            throw new InvalidDataException("Snapshot session seed cannot be negative.");
        }

        if (snapshot.PlayerEntityId is null && snapshot.PlayerProfile is null)
        {
            return;
        }

        if (snapshot.SessionSeed is null
            || string.IsNullOrWhiteSpace(snapshot.PlayerEntityId)
            || snapshot.PlayerProfile is not { IsValid: true }
            || !entityIds.Contains(snapshot.PlayerEntityId))
        {
            throw new InvalidDataException("Snapshot session player metadata is incomplete or invalid.");
        }

        var player = snapshot.Entities.First(entity =>
            string.Equals(entity.Id, snapshot.PlayerEntityId, StringComparison.Ordinal));
        if (!string.Equals(player.SpeciesId, "human", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Snapshot session player must reference a human entity.");
        }
    }

    private static void ValidateRelationships(
        IReadOnlyList<RelationshipSnapshot> relationships,
        HashSet<string> entityIds)
    {
        var relationshipPairs = new HashSet<(string ObserverId, string TargetId)>();
        foreach (var relationship in relationships)
        {
            if (relationship is null
                || string.IsNullOrWhiteSpace(relationship.ObserverEntityId)
                || string.IsNullOrWhiteSpace(relationship.TargetEntityId)
                || string.Equals(
                    relationship.ObserverEntityId,
                    relationship.TargetEntityId,
                    StringComparison.Ordinal)
                || !entityIds.Contains(relationship.ObserverEntityId)
                || !entityIds.Contains(relationship.TargetEntityId))
            {
                throw new InvalidDataException(
                    "Snapshot relationships must reference two distinct existing entity IDs.");
            }

            if (!relationshipPairs.Add(
                (relationship.ObserverEntityId, relationship.TargetEntityId)))
            {
                throw new InvalidDataException(
                    $"Snapshot contains duplicate relationship '{relationship.ObserverEntityId}' to '{relationship.TargetEntityId}'.");
            }

            ValidateRelationshipValue(relationship.Familiarity, "familiarity", relationship);
            ValidateRelationshipValue(relationship.Trust, "trust", relationship);
            ValidateRelationshipValue(relationship.Fear, "fear", relationship);
            ValidateRelationshipValue(relationship.Loyalty, "loyalty", relationship);
        }
    }

    private static void ValidateRelationshipValue(
        double value,
        string dimension,
        RelationshipSnapshot relationship)
    {
        if (!double.IsFinite(value) || value is < 0 or > 100)
        {
            throw new InvalidDataException(
                $"Snapshot relationship '{relationship.ObserverEntityId}' to '{relationship.TargetEntityId}' has {dimension} outside [0, 100].");
        }
    }

    private static void ValidateLocations(
        IReadOnlyList<ResourceLocationSnapshot> locations,
        string kind,
        HashSet<string> targetIds,
        HashSet<string> factionIds,
        bool allowFactionOwner)
    {
        foreach (var location in locations)
        {
            if (location is null || string.IsNullOrWhiteSpace(location.Id)
                || !targetIds.Add(location.Id))
            {
                throw new InvalidDataException($"Snapshot contains an invalid or duplicate {kind} ID.");
            }

            if (location.FactionOwnerId is { } factionId
                && (!allowFactionOwner || !factionIds.Contains(factionId)))
            {
                throw new InvalidDataException(
                    $"Snapshot {kind} '{location.Id}' references an unknown or invalid faction owner.");
            }

            ValidatePosition(location.Position, $"{kind} '{location.Id}' position");
            ValidateContainer(location.Contents, location.Id);
        }
    }

    private static void ValidateInventory(
        IReadOnlyDictionary<string, int>? inventory,
        int capacity,
        string entityId)
    {
        if (inventory is null)
        {
            throw new InvalidDataException($"Snapshot entity '{entityId}' inventory is required.");
        }

        var usedCapacity = 0L;
        foreach (var item in inventory)
        {
            ValidateItemCount(item.Key, item.Value, $"entity '{entityId}' inventory");
            usedCapacity += item.Value;
        }

        if (usedCapacity > capacity)
        {
            throw new InvalidDataException(
                $"Snapshot entity '{entityId}' inventory exceeds capacity {capacity}.");
        }
    }

    private static void ValidateContainer(ResourceContainerSnapshot? container, string sourceId)
    {
        if (container is null || string.IsNullOrWhiteSpace(container.Name))
        {
            throw new InvalidDataException($"Snapshot food source '{sourceId}' has invalid contents.");
        }

        if (container.Capacity < 0 || container.Counts is null)
        {
            throw new InvalidDataException($"Snapshot food source '{sourceId}' has invalid capacity or counts.");
        }

        var usedCapacity = 0L;
        foreach (var item in container.Counts)
        {
            ValidateItemCount(item.Key, item.Value, $"food source '{sourceId}'");
            usedCapacity += item.Value;
        }

        if (usedCapacity > container.Capacity)
        {
            throw new InvalidDataException(
                $"Snapshot food source '{sourceId}' contents exceed capacity {container.Capacity}.");
        }
    }

    private static void ValidateItemCount(string itemId, int count, string owner)
    {
        if (!ItemIds.IsKnown(itemId))
        {
            throw new InvalidDataException($"Snapshot {owner} references unknown item '{itemId}'.");
        }

        if (count < 0)
        {
            throw new InvalidDataException($"Snapshot {owner} contains a negative count for '{itemId}'.");
        }
    }

    private static void ValidatePosition(Vector3 position, string owner)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
        {
            throw new InvalidDataException($"Snapshot {owner} must have finite coordinates.");
        }
    }

    private static long ParseEntityId(string id)
    {
        const string prefix = "entity-";
        if (!id.StartsWith(prefix, StringComparison.Ordinal)
            || !long.TryParse(id.AsSpan(prefix.Length), out var number)
            || number <= 0)
        {
            throw new InvalidDataException($"Snapshot entity ID '{id}' is invalid.");
        }

        return number;
    }

    private static ResourceContainerSnapshot CaptureContainer(ResourceContainer container)
    {
        return new ResourceContainerSnapshot(
            container.Name,
            container.Capacity,
            new Dictionary<string, int>(container.Counts, StringComparer.Ordinal));
    }

    private static ResourceLocationSnapshot CaptureLocation(ResourceLocation location) =>
        new(location.Id, location.Position, CaptureContainer(location.Contents), location.FactionOwnerId);
}
