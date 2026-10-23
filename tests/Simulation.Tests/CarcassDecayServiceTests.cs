using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class CarcassDecayServiceTests
{
    private static readonly SpeciesCatalog Catalog = SpeciesCatalog.LoadFromFile(
        Path.Combine(AppContext.BaseDirectory, "data", "species.json"));

    [Fact]
    public void ExpiresPerishableSourceAtBoundaryAndOnlyOnce()
    {
        var world = new World();
        var carcass = AddFood(world, "carcass-cow", 5);
        var service = new CarcassDecayService(world);

        Assert.Empty(service.Advance(0));
        Assert.Equal(5, carcass.FreshnessSecondsRemaining);
        Assert.Empty(service.Advance(4.9));
        Assert.Equal(0.1, carcass.FreshnessSecondsRemaining!.Value, precision: 10);
        Assert.Equal(new[] { carcass.Id }, service.Advance(0.1));
        Assert.False(world.TryGetFoodSource(carcass.Id, out _));
        Assert.Empty(service.Advance(1));
    }

    [Fact]
    public void DoesNotExpireOrdinaryFoodAndCanConsumeCarcassBeforeExpiry()
    {
        var world = new World();
        var ordinaryFoodContents = new ResourceContainer("food", 5);
        ordinaryFoodContents.TryAdd(ItemIds.Food, 5);
        var ordinaryFood = world.AddFoodSource("food", Vector3.Zero, ordinaryFoodContents);
        var carcass = AddFood(world, "carcass-cow", 5);
        var service = new CarcassDecayService(world);
        var consumer = world.SpawnEntity("wolf", Vector3.Zero, satiety: 20);
        var navigator = new FakeNavigationAdapter();
        var feeding = new FoodConsumptionService(world, Catalog, _ => navigator);

        Assert.Equal(FoodConsumptionStatus.Traveling, feeding.Advance(consumer, simulationTick: 1).Status);
        Assert.True(navigator.CompleteAsArrived(navigator.Requests.Keys.Single()));
        Assert.Equal(FoodConsumptionStatus.Ate, feeding.Advance(consumer, simulationTick: 2).Status);
        Assert.Equal(4, carcass.Contents.Counts[ItemIds.Meat]);
        Assert.Empty(service.Advance(4.9));
        Assert.Equal(new[] { carcass.Id }, service.Advance(0.1));
        Assert.True(world.TryGetFoodSource(ordinaryFood.Id, out _));
    }

    [Fact]
    public void ExpiresCarcassDuringTravelAndSelectsAnotherFoodSource()
    {
        var world = new World();
        var carcass = AddFood(world, "carcass-cow", 0.1, new Vector3(1, 0, 0));
        var alternate = AddFood(world, "alternate-meat", null, new Vector3(5, 0, 0));
        var wolf = world.SpawnEntity("wolf", Vector3.Zero, satiety: 20);
        var navigator = new FakeNavigationAdapter();
        var feeding = new FoodConsumptionService(world, Catalog, _ => navigator);
        var decay = new CarcassDecayService(world);

        var first = feeding.Advance(wolf, simulationTick: 1);
        var initialRequestId = navigator.Requests.Keys.Single();
        Assert.Equal(FoodConsumptionStatus.Traveling, first.Status);
        Assert.Equal(carcass, first.Source);

        Assert.Equal(new[] { carcass.Id }, decay.Advance(0.1));
        var rerouted = feeding.Advance(wolf, simulationTick: 2);

        Assert.Equal(FoodConsumptionStatus.Traveling, rerouted.Status);
        Assert.Equal(alternate, rerouted.Source);
        Assert.Equal(NavigationOutcome.Cancelled, navigator.Poll(initialRequestId, 0));
        Assert.False(world.TryGetFoodSource(carcass.Id, out _));
    }

    [Fact]
    public void RejectsInvalidElapsedTime()
    {
        var service = new CarcassDecayService(new World());
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Advance(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Advance(double.NaN));
    }

    [Fact]
    public void FreshnessFollowsClockPauseAndFourTimesSpeed()
    {
        var world = new World();
        var carcass = AddFood(world, "carcass-cow", 2);
        var decay = new CarcassDecayService(world);
        var clock = new SimulationClock();

        clock.Advance(0.5, seconds => decay.Advance(seconds));
        Assert.Equal(1.5, carcass.FreshnessSecondsRemaining!.Value, precision: 10);

        clock.SetPaused(true);
        clock.Advance(10, seconds => decay.Advance(seconds));
        Assert.Equal(1.5, carcass.FreshnessSecondsRemaining!.Value, precision: 10);

        clock.SetPaused(false);
        clock.SetSpeed(SimulationSpeed.FourX);
        clock.Advance(0.125, seconds => decay.Advance(seconds));
        Assert.Equal(1.0, carcass.FreshnessSecondsRemaining!.Value, precision: 10);
    }

    private static FoodSource AddFood(World world, string id, double? freshness, Vector3 position = default)
    {
        var contents = new ResourceContainer(id, 5);
        contents.TryAdd(ItemIds.Meat, 5);
        return world.AddFoodSource(id, position, contents, freshness);
    }

}
