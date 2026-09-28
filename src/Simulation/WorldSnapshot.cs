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

        var snapshot = JsonSerializer.Deserialize<WorldSnapshot>(json, JsonOptions)
            ?? throw new InvalidDataException("Snapshot JSON did not contain a snapshot.");
        if (snapshot.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported world snapshot schema version {snapshot.SchemaVersion}.");
        }

        return snapshot;
    }

    private static ResourceContainerSnapshot CaptureContainer(ResourceContainer container)
    {
        return new ResourceContainerSnapshot(
            container.Name,
            container.Capacity,
            new Dictionary<string, int>(container.Counts, StringComparer.Ordinal));
    }
}
