using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class WorldSnapshotTests
{
    [Fact]
    public void SnapshotCapturePreservesDurableWorldStateAndCarriedWood()
    {
        var world = new World();
        var sourceContents = new ResourceContainer("food-pile", 10);
        sourceContents.TryAdd(ItemIds.Food, 7);
        sourceContents.TryAdd(ItemIds.Wood, 2);
        world.AddFoodSource("food-pile", new Vector3(2, 0, 3), sourceContents);
        var entity = world.SpawnEntity("human", new Vector3(1, 0, -2), 47.5);
        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryPickUp(sourceContents, entity, ItemIds.Wood, 2));
        var snapshot = WorldSnapshotSerializer.Capture(world, 5.5);

        Assert.Equal(5.5, snapshot.SimulationTime);
        Assert.Equal(entity.Id, snapshot.Entities.Single().Id);
        Assert.Equal(new Vector3(1, 0, -2), snapshot.Entities.Single().Position);
        Assert.Equal(47.5, snapshot.Entities.Single().Satiety);
        Assert.Equal(2, snapshot.Entities.Single().Inventory[ItemIds.Wood]);
        Assert.Equal(7, snapshot.FoodSources.Single().Contents.Counts[ItemIds.Food]);
    }

    [Fact]
    public void SnapshotIncludesEntityInventoryAndExcludesTransientActionState()
    {
        var world = new World();
        var sourceContents = new ResourceContainer("wood-pile", 10);
        sourceContents.TryAdd(ItemIds.Wood, 4);
        world.AddFoodSource("wood-pile", Vector3.Zero, sourceContents);
        var entity = world.SpawnEntity("human", Vector3.One);
        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryPickUp(sourceContents, entity, ItemIds.Wood, 3));
        entity.Actions.TryStart(new ActionCandidate("haul-1", ActionKind.Haul));

        var json = WorldSnapshotSerializer.Serialize(world, 12.3);
        var snapshot = WorldSnapshotSerializer.Deserialize(json);

        Assert.Equal(WorldSnapshotSerializer.CurrentSchemaVersion, snapshot.SchemaVersion);
        Assert.Equal(12.3, snapshot.SimulationTime);
        Assert.Equal(entity.Id, snapshot.Entities.Single().Id);
        Assert.Equal(Vector3.One, snapshot.Entities.Single().Position);
        Assert.Equal(3, snapshot.Entities.Single().Inventory[ItemIds.Wood]);
        Assert.Contains("wood-pile", json);
        Assert.DoesNotContain("haul-1", json);
    }

    [Fact]
    public void SnapshotRejectsUnsupportedVersionsAndInvalidSimulationTime()
    {
        var world = new World();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => WorldSnapshotSerializer.Capture(world, double.NaN));

        var json = WorldSnapshotSerializer.Serialize(world, 0)
            .Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 99", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Deserialize(json));
    }

    [Fact]
    public void LoadAppliesValidSnapshotAfterCompleteValidation()
    {
        var sourceWorld = new World();
        var contents = new ResourceContainer("food-pile", 10);
        contents.TryAdd(ItemIds.Food, 4);
        sourceWorld.AddFoodSource("food-pile", new Vector3(3, 0, 2), contents);
        var sourceEntity = sourceWorld.SpawnEntity("creature", new Vector3(1, 0, -2), 42);
        var json = WorldSnapshotSerializer.Serialize(sourceWorld, 8.5);

        var targetWorld = new World();
        targetWorld.SpawnEntity("human", Vector3.Zero, 90);

        var simulationTime = WorldSnapshotSerializer.Load(
            targetWorld,
            json,
            GetSpeciesCatalog());

        Assert.Equal(8.5, simulationTime);
        var restored = Assert.Single(targetWorld.Entities);
        Assert.Equal(sourceEntity.Id, restored.Id);
        Assert.Equal("creature", restored.SpeciesId);
        Assert.Equal(new Vector3(1, 0, -2), restored.Position);
        Assert.Equal(42, restored.Satiety);
        Assert.Equal(4, targetWorld.FoodSources.Single().Contents.Counts[ItemIds.Food]);
        Assert.Equal("entity-000002", targetWorld.SpawnEntity("human", Vector3.One).Id);
    }

    [Fact]
    public void InvalidSnapshotLeavesExistingWorldUnchanged()
    {
        var world = CreatePopulatedWorld();
        var originalEntity = Assert.Single(world.Entities);
        var originalPosition = originalEntity.Position;
        var originalSatiety = originalEntity.Satiety;
        var originalFood = world.FoodSources.Single().Contents.Counts[ItemIds.Food];
        var validJson = WorldSnapshotSerializer.Serialize(world, 3);

        var invalidSnapshots = new[]
        {
            "{",
            validJson.Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 99", StringComparison.Ordinal),
            validJson.Replace("\"SpeciesId\": \"human\"", "\"SpeciesId\": \"unknown\"", StringComparison.Ordinal),
            validJson.Replace("\"food\": 3", "\"food\": -1", StringComparison.Ordinal)
        };

        foreach (var invalidJson in invalidSnapshots)
        {
            Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
                world,
                invalidJson,
                GetSpeciesCatalog()));

            Assert.Equal(originalPosition, originalEntity.Position);
            Assert.Equal(originalSatiety, originalEntity.Satiety);
            Assert.Equal(originalFood, world.FoodSources.Single().Contents.Counts[ItemIds.Food]);
            Assert.Same(originalEntity, Assert.Single(world.Entities));
        }
    }

    [Fact]
    public void DuplicateEntityIdsAreRejectedBeforeWorldMutation()
    {
        var world = CreatePopulatedWorld();
        world.SpawnEntity("creature", Vector3.One, 45);
        var sourceSnapshot = WorldSnapshotSerializer.Serialize(world, 3);
        var duplicateId = sourceSnapshot.Replace(
            "\"Id\": \"entity-000002\"",
            "\"Id\": \"entity-000001\"",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
            world,
            duplicateId,
            GetSpeciesCatalog()));
        Assert.Equal(2, world.Entities.Count);
    }

    private static World CreatePopulatedWorld()
    {
        var world = new World();
        var contents = new ResourceContainer("food-pile", 10);
        contents.TryAdd(ItemIds.Food, 3);
        world.AddFoodSource("food-pile", Vector3.UnitX, contents);
        world.SpawnEntity("human", Vector3.Zero, 55);
        return world;
    }

    private static SpeciesCatalog GetSpeciesCatalog()
    {
        return SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
    }
}
