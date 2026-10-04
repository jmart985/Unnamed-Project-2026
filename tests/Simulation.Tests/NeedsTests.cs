using Simulation;

namespace Simulation.Tests;

public sealed class NeedsTests
{
    [Fact]
    public void SatietyDrainsBySimulationTimeAndClampsAtZero()
    {
        var needs = new NeedsState(new NeedsTuning(satietyDrainPerSecond: 0.2));

        needs.Advance(100);

        Assert.Equal(40, needs.Satiety, precision: 6);
        needs.Advance(200);
        Assert.Equal(0, needs.Satiety);
        Assert.True(needs.IsStarving);
    }

    [Fact]
    public void EqualSimulationTimeProducesEqualSatietyAcrossTickSizes()
    {
        var oneLargeStep = new NeedsState(new NeedsTuning(satietyDrainPerSecond: 0.2));
        var manySmallSteps = new NeedsState(new NeedsTuning(satietyDrainPerSecond: 0.2));

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
        var needs = new NeedsState(new NeedsTuning(initialSatiety: 31, satietyDrainPerSecond: 1, hungryThreshold: 30));

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
        Assert.Throws<ArgumentOutOfRangeException>(() => new NeedsTuning(satietyDrainPerSecond: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NeedsTuning(hungryThreshold: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NeedsTuning(nutritionPerUnit: 0));

        var needs = new NeedsState(new NeedsTuning(satietyDrainPerSecond: 0.2));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.Advance(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.AddNutrition(0));
    }
}
