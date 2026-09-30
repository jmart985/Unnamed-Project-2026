using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class EntityInspectionReadModelTests
{
    [Fact]
    public void SnapshotContainsIdleAndInventoryStateWithoutMutationAccess()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.One, 72);
        var snapshot = EntityInspectionReadModel.Create(entity);

        Assert.Equal(entity.Id, snapshot.Id);
        Assert.Equal("human", snapshot.SpeciesId);
        Assert.Equal(Vector3.One, snapshot.Position);
        Assert.Equal(72, snapshot.Satiety);
        Assert.Equal("Idle", snapshot.Activity);
        Assert.Null(snapshot.Target);
        Assert.Empty(snapshot.Inventory);
    }

    [Fact]
    public void SnapshotReflectsHungryHaulingAndWaitingStates()
    {
        var world = new World();
        var hungry = world.SpawnEntity("human", Vector3.Zero, 10);
        hungry.Needs.Advance(0);
        var haul = world.SpawnEntity("human", Vector3.One);
        Assert.Equal(ActionStartResult.Started, haul.Actions.TryStart(new ActionCandidate("haul-1", ActionKind.Haul)));
        var waiting = world.SpawnEntity("human", Vector3.UnitX, 10);
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var selection = new FoodSelectionService(world, catalog);
        var result = selection.TrySelect(waiting, [], 0);

        Assert.Equal(FoodSelectionStatus.Waiting, result.Status);
        Assert.True(EntityInspectionReadModel.Create(hungry).Satiety < 30);
        Assert.Equal("haul-1", EntityInspectionReadModel.Create(haul).Target);
        var waitingSnapshot = EntityInspectionReadModel.Create(waiting);
        Assert.Equal("Waiting", waitingSnapshot.Activity);
        Assert.NotNull(waitingSnapshot.WaitingReason);
    }
}
