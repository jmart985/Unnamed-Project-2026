using Simulation;

namespace Simulation.Tests;

public sealed class ScenarioAcceptanceHarnessTests
{
    [Fact]
    public void RunUsesSeedAndBoundedTicksAndProducesMachineReadableOutput()
    {
        var result = ScenarioAcceptanceHarness.Run(Catalog(), seed: 17, ticks: 12);

        Assert.True(result.Passed);
        Assert.Equal(17, result.Seed);
        Assert.Equal(12, result.Ticks);
        Assert.Equal(12, result.FirstRun.SimulationTicks);
        Assert.Null(result.FirstMismatchField);
        Assert.Contains("\"Passed\":true", result.ToJson());
    }

    [Fact]
    public void NegativeTickLimitIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ScenarioAcceptanceHarness.Run(Catalog(), ticks: -1));
    }

    private static SpeciesCatalog Catalog() => SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
}
