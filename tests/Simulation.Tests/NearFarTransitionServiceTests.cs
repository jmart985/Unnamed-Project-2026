using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class NearFarTransitionServiceTests
{
    [Fact]
    public void PromotesAndDemotesARegionWithoutDuplicatingCounts()
    {
        var world = new World();
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var transitions = new NearFarTransitionService(world, catalog);
        transitions.SetFarRecord(new("grove", "creature", 2, 40, []));

        var near = transitions.Promote("grove", Vector3.Zero);
        Assert.Equal(2, near.Count);
        Assert.All(near, entity =>
        {
            Assert.Equal(80, entity.BaseStats.MaxHealth);
            Assert.Equal(2, entity.BaseStats.MovementSpeed);
            Assert.Equal(1.2, entity.BaseStats.Endurance);
        });
        Assert.Empty(transitions.FarRecords);
        Assert.True(transitions.Demote("grove"));
        Assert.Empty(world.Entities);
        Assert.Equal(2, transitions.FarRecords["grove"].Count);
    }

    [Fact]
    public void EmptyOrInvalidRegionsAreNoOps()
    {
        var world = new World();
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var transitions = new NearFarTransitionService(world, catalog);
        Assert.Empty(transitions.Promote("missing", Vector3.Zero));
        Assert.False(transitions.Demote("missing"));
    }
}
