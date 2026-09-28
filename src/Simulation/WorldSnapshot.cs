using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Simulation;

public sealed record WorldSnapshot(
    int SchemaVersion,
    double SimulationTime,
    long LastAllocatedEntityId,
    IReadOnlyList<EntitySnapshot> Entities,
    IReadOnlyList<FoodSourceSnapshot> FoodSources);

public sealed record EntitySnapshot(
    string Id,
    string SpeciesId,
    Vector3 Position,
    double Satiety,
    IReadOnlyDictionary<string, int> Inventory);

public sealed record FoodSourceSnapshot(
    string Id,
    Vector3 Position,
    ResourceContainerSnapshot Contents);

public sealed record ResourceContainerSnapshot(
    string Name,
    int Capacity,
    IReadOnlyDictionary<string, int> Counts);

public static class WorldSnapshotSerializer
{
    public const int CurrentSchemaVersion = 1;

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
                new Dictionary<string, int>(entity.Inventory, StringComparer.Ordinal)))
            .ToArray();
        var foodSources = world.FoodSources
            .Select(source => new FoodSourceSnapshot(
                source.Id,
                source.Position,
                CaptureContainer(source.Contents)))
            .ToArray();

        return new WorldSnapshot(
            CurrentSchemaVersion,
            simulationTime,
            world.LastAllocatedEntityId,
            entities,
            foodSources);
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
        SpeciesCatalog speciesCatalog)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(speciesCatalog);

        var snapshot = Deserialize(json);
        Validate(snapshot, target, speciesCatalog);

        target.ApplySnapshot(snapshot);
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

        if (snapshot.Entities is null || snapshot.FoodSources is null)
        {
            throw new InvalidDataException("Snapshot entity and food source lists are required.");
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

            ValidateInventory(entity.Inventory, target.InventoryCapacity, entity.Id);
            highestEntityId = Math.Max(highestEntityId, ParseEntityId(entity.Id));
        }

        if (snapshot.LastAllocatedEntityId < highestEntityId)
        {
            throw new InvalidDataException(
                "Snapshot last allocated entity ID must include every entity ID.");
        }

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
}
