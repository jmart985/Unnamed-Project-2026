using Simulation;

namespace Simulation.Tests;

public sealed class FarEntitySimulationServiceTests
{
    private static FarEntitySimulationService CreateService() => new(SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json")));

    [Fact]
    public void ZeroPopulationRemainsZeroAndIsStable()
    {
        var input = new FarEntitySimulationRequest(new FarEntityRecord("grove", "creature", 0, 60, []), 100, 7);
        var service = CreateService();
        Assert.Equal(service.Advance(input), service.Advance(input));
        Assert.Equal(0, service.Advance(input).Record.Count);
    }

    [Fact]
    public void StarvationPressureRemovesPopulationWithoutGoingNegative()
    {
        var result = CreateService().Advance(new(new FarEntityRecord("grove", "creature", 4, 1, []), 100, 3));
        Assert.True(result.Deaths > 0);
        Assert.InRange(result.Record.Count, 0, 4);
    }

    [Fact]
    public void FoodAndMigrationAreDeterministicAggregateChanges()
    {
        var input = new FarEntitySimulationRequest(new FarEntityRecord("grove", "creature", 20, 70, [new(ItemIds.Food, 20)]), 10, 99, 40, 100, "marsh");
        var first = CreateService().Advance(input);
        var second = CreateService().Advance(input);
        Assert.Equal(first, second);
        Assert.True(first.Migrated);
        Assert.Equal("marsh", first.Record.RegionId);
        Assert.True(first.Births > 0);
        Assert.Equal(20, first.FoodConsumed);
    }

    [Fact]
    public void BirthFoodThresholdUsesWiderArithmetic()
    {
        var input = new FarEntitySimulationRequest(
            new FarEntityRecord("grove", "creature", int.MaxValue, 60, []),
            0, 1, int.MaxValue);

        var result = CreateService().Advance(input);

        Assert.Equal(0, result.Births);
        Assert.Equal(int.MaxValue, result.Record.Count);
    }
}
