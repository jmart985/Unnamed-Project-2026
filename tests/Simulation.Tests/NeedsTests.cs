using Simulation;

namespace Simulation.Tests;

public sealed class NeedsTests
{
    [Fact]
    public void SatietyDrainsBySimulationTimeAndClampsAtZero()
    {
        var needs = new NeedsState(new NeedsTuning());

        needs.Advance(100);

        Assert.Equal(40, needs.Satiety, precision: 6);
        needs.Advance(200);
        Assert.Equal(0, needs.Satiety);
        Assert.True(needs.IsStarving);
    }

    [Fact]
    public void EqualSimulationTimeProducesEqualSatietyAcrossTickSizes()
    {
        var oneLargeStep = new NeedsState(new NeedsTuning());
        var manySmallSteps = new NeedsState(new NeedsTuning());

        oneLargeStep.Advance(10);
        for (var index = 0; index < 100; index++)
        {
            manySmallSteps.Advance(0.1);
        }

        Assert.Equal(oneLargeStep.Satiety, manySmallSteps.Satiety, precision: 6);
    }

    [Fact]
    public void HungerBeginsAtOrBelowConfiguredThreshold()
    {
        var needs = new NeedsState(new NeedsTuning(initialSatiety: 31, drainPerSecond: 1, hungryThreshold: 30));

        Assert.False(needs.IsHungry);
        needs.Advance(1);

        Assert.Equal(30, needs.Satiety);
        Assert.True(needs.IsHungry);
        Assert.False(needs.IsStarving);
    }

    [Fact]
    public void NutritionClampsAtOneHundredAndRecoversStarvation()
    {
        var needs = new NeedsState(new NeedsTuning(initialSatiety: 0));

        Assert.True(needs.IsStarving);
        needs.AddNutrition(50);
        Assert.Equal(50, needs.Satiety);
        Assert.False(needs.IsStarving);

        needs.AddNutrition(100);
        Assert.Equal(100, needs.Satiety);
    }

    [Fact]
    public void InvalidTuningAndUpdatesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NeedsTuning(initialSatiety: 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NeedsTuning(drainPerSecond: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NeedsTuning(hungryThreshold: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NeedsTuning(nutritionPerUnit: 0));

        var needs = new NeedsState(new NeedsTuning());
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.Advance(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.AddNutrition(0));
    }

    [Fact]
    public void SimulationClockAdvancesWorldNeedsButPauseFreezesThem()
    {
        var world = new World(new NeedsTuning(drainPerSecond: 1));
        var entity = world.SpawnEntity("human", System.Numerics.Vector3.Zero, 50);
        var clock = new SimulationClock();

        Assert.Equal(2, clock.Advance(0.2, world.AdvanceNeeds));
        Assert.Equal(49.8, entity.Satiety, precision: 6);

        clock.SetPaused(true);
        Assert.Equal(0, clock.Advance(1, world.AdvanceNeeds));
        Assert.Equal(49.8, entity.Satiety, precision: 6);

        clock.SetPaused(false);
        Assert.Equal(1, clock.Advance(0.1, world.AdvanceNeeds));
        Assert.Equal(49.7, entity.Satiety, precision: 6);
    }
}
