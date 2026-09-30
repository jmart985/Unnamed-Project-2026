using Simulation;

namespace Simulation.Tests;

public sealed class AntFarmScenarioTests
{
    [Fact]
    public void ResetCreatesTheDocumentedStartingState()
    {
        var scenario = CreateScenario();
        var summary = scenario.Summarize();

        Assert.Equal(10, summary.HumanCount);
        Assert.Equal(3, summary.CreatureCount);
        Assert.Equal(100, summary.FoodUnits);
        Assert.Equal(100, summary.WoodUnits);
        Assert.Equal(0, summary.StoredWoodUnits);
        Assert.Equal(0, summary.SimulationTicks);
    }

    [Fact]
    public void RepeatedResetAndAdvanceUseStableDeterministicState()
    {
        var first = CreateScenario();
        first.Advance(25);
        var firstSummary = first.Summarize();
        first.Reset();
        first.Advance(25);

        Assert.Equal(firstSummary, first.Summarize());
        Assert.Equal(firstSummary, CreateScenarioAfterAdvance(25).Summarize());
    }

    [Fact]
    public void NegativeAdvanceIsRejectedAndDoesNotChangeState()
    {
        var scenario = CreateScenario();
        Assert.Throws<ArgumentOutOfRangeException>(() => scenario.Advance(-1));
        Assert.Equal(0, scenario.SimulationTicks);
    }

    private static AntFarmScenario CreateScenario()
    {
        return new(SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json")));
    }

    private static AntFarmScenario CreateScenarioAfterAdvance(int ticks)
    {
        var scenario = CreateScenario();
        scenario.Advance(ticks);
        return scenario;
    }
}
