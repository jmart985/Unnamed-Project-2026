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
    IReadOnlyList<FactionHostilitySnapshot>? FactionHostilities = null);

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
    string? AllegianceFactionId = null);

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
    public const int CurrentSchemaVersion = 8;

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
                entity.AllegianceFactionId))
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
            factionHostilities);
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

        if (snapshot.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported world snapshot schema version {snapshot.SchemaVersion}.");
        }

        return snapshot;
    }

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
        ValidateRelationships(snapshot.Relationships, entityIds);
        ValidateFactionReputations(snapshot.FactionReputations, entityIds, factionIds);
        ValidateFactionHostilities(snapshot.FactionHostilities, factionIds);
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
