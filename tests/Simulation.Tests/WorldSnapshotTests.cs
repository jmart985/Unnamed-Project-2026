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
}
