using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class SimulationSaveBoundaryTests
{
    [Fact]
    public void SaveRequiresPausedClockAndRoundTripsCarriedInventory()
    {
        var source = new World();
        var entity = source.SpawnEntity("human", Vector3.One);
        var pile = new ResourceContainer("wood", 10);
        Assert.Equal(ResourceMutationResult.Success, pile.TryAdd(ItemIds.Wood, 3));
        Assert.Equal(ResourceMutationResult.Success, ResourceTransferService.TryPickUp(pile, entity, ItemIds.Wood, 3));
        var clock = new SimulationClock();
        var boundary = new SimulationSaveBoundary();
        Assert.Equal(SaveBoundaryStatus.RequiresPausedBoundary, boundary.Save(source, clock).Status);

        clock.SetPaused(true);
        var saved = boundary.Save(source, clock);
        Assert.Equal(SaveBoundaryStatus.Accepted, saved.Status);

        var target = new World();
        var targetClock = new SimulationClock();
        targetClock.SetPaused(true);
        var loaded = boundary.Load(target, targetClock, saved.Snapshot!, Catalog());

        Assert.Equal(SaveBoundaryStatus.Accepted, loaded.Status);
        Assert.Equal(3, target.Entities.Single().Inventory[ItemIds.Wood]);
        Assert.True(targetClock.IsPaused);
    }

    [Fact]
    public void InvalidSnapshotReturnsStableErrorAndLeavesWorldUnchanged()
    {
        var world = new World();
        world.SpawnEntity("human", Vector3.One);
        var clock = new SimulationClock();
        clock.SetPaused(true);
        var boundary = new SimulationSaveBoundary();

        var result = boundary.Load(world, clock, "{\"schemaVersion\":999}", Catalog());

        Assert.Equal(SaveBoundaryStatus.InvalidSnapshot, result.Status);
        Assert.Equal("invalid_snapshot", result.ErrorCode);
        Assert.Single(world.Entities);
        Assert.Equal(0, clock.SimulationTime);
    }

    private static SpeciesCatalog Catalog() => SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
}
