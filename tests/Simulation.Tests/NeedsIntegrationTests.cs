using Simulation;

namespace Simulation.Tests;

public sealed class NeedsIntegrationTests
{
    [Fact]
    public void PausedClockDoesNotAdvanceWorldNeeds()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", System.Numerics.Vector3.Zero);
        var clock = new SimulationClock();
        clock.SetPaused(true);

        Assert.Equal(0, clock.Advance(10, seconds => entity.Needs.Advance(seconds)));
        Assert.Equal(60, entity.Satiety);
        Assert.False(entity.Needs.IsHungry);
    }

    [Fact]
    public void ActiveClockUpdatesNeedsAndExposesHungerState()
    {
        var world = new World(new NeedsTuning(satietyDrainPerSecond: 10));
        var entity = world.SpawnEntity("human", System.Numerics.Vector3.Zero);
        var clock = new SimulationClock();

        Assert.Equal(3, clock.Advance(0.3, seconds => entity.Needs.Advance(seconds)));
        Assert.Equal(57, entity.Satiety, precision: 6);
        Assert.False(entity.Needs.IsHungry);

        entity.Needs.AddNutrition(50);
        Assert.Equal(100, entity.Satiety, precision: 6);
    }
}
