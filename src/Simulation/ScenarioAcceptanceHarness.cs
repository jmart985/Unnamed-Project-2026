using System.Text.Json;

namespace Simulation;

public sealed record ScenarioAcceptanceResult(
    bool Passed,
    int Seed,
    int Ticks,
    ScenarioStateSummary FirstRun,
    ScenarioStateSummary SecondRun,
    string? FirstMismatchField)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = false });
}

/// <summary>Runs the bounded deterministic scenario twice and reports the first differing state field.</summary>
public static class ScenarioAcceptanceHarness
{
    public static ScenarioAcceptanceResult Run(SpeciesCatalog catalog, int seed = AntFarmScenario.DefaultSeed, int ticks = 25)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));

        var first = CreateScenario(catalog, seed, ticks);
        var second = CreateScenario(catalog, seed, ticks);
        var mismatch = FirstMismatch(first, second);
        return new(mismatch is null, seed, ticks, first, second, mismatch);
    }

    private static ScenarioStateSummary CreateScenario(SpeciesCatalog catalog, int seed, int ticks)
    {
        var scenario = new AntFarmScenario(catalog, seed);
        scenario.Advance(ticks);
        return scenario.Summarize();
    }

    private static string? FirstMismatch(ScenarioStateSummary first, ScenarioStateSummary second)
    {
        if (first.HumanCount != second.HumanCount) return nameof(ScenarioStateSummary.HumanCount);
        if (first.CreatureCount != second.CreatureCount) return nameof(ScenarioStateSummary.CreatureCount);
        if (first.FoodUnits != second.FoodUnits) return nameof(ScenarioStateSummary.FoodUnits);
        if (first.WoodUnits != second.WoodUnits) return nameof(ScenarioStateSummary.WoodUnits);
        if (first.StoredWoodUnits != second.StoredWoodUnits) return nameof(ScenarioStateSummary.StoredWoodUnits);
        if (first.SimulationTicks != second.SimulationTicks) return nameof(ScenarioStateSummary.SimulationTicks);
        return null;
    }
}
