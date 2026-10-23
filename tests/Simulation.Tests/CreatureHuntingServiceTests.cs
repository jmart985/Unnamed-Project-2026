using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class CreatureHuntingServiceTests
{
    private static readonly SpeciesCatalog Catalog = SpeciesCatalog.LoadFromFile(
        Path.Combine(AppContext.BaseDirectory, "data", "species.json"));

    [Fact]
    public void HungryWolfSelectsNearestConfiguredWildPreyWithStableTieBreakAndEatsCarcass()
    {
        var world = new World();
        var wolf = world.SpawnEntity(Catalog.GetRequired("wolf"), Vector3.Zero, satiety: 20);
        var firstCow = world.SpawnEntity(Catalog.GetRequired("cow"), new Vector3(1, 0, 0));
        var tiedCow = world.SpawnEntity(Catalog.GetRequired("cow"), new Vector3(-1, 0, 0));
        var farCow = world.SpawnEntity(Catalog.GetRequired("cow"), new Vector3(4, 0, 0));
        var navigator = new FakeNavigationAdapter();
        var hunting = new CreatureHuntingService(world, Catalog, _ => navigator);
        var feeding = new FoodConsumptionService(world, Catalog, _ => navigator);

        var selected = hunting.Advance(wolf, simulationTick: 1);

        Assert.Equal(CreatureHuntingStatus.Attacked, selected.Status);
        Assert.Equal(firstCow.Id, selected.TargetEntityId);
        Assert.Equal(firstCow.Id, wolf.TargetId);
        Assert.NotNull(wolf.Actions.Current);
        Assert.Equal(ActionKind.Hunt, wolf.Actions.Current!.Kind);
        Assert.True(firstCow.Health.TryGetPart("Torso", out var torso));
        Assert.True(torso!.CurrentHealth < torso.MaximumHealth);
        Assert.Equal(torso.MaximumHealth, tiedCow.Health.Parts.Single(part => part.Name == "Torso").CurrentHealth);
        Assert.Equal(torso.MaximumHealth, farCow.Health.Parts.Single(part => part.Name == "Torso").CurrentHealth);

        CreatureHuntingResult result = selected;
        for (var tick = 2; tick <= 40 && result.Status != CreatureHuntingStatus.Harvested; tick++)
            result = hunting.Advance(wolf, tick);

        Assert.Equal(CreatureHuntingStatus.Harvested, result.Status);
        Assert.Equal(firstCow.Id, result.TargetEntityId);
        var carcass = Assert.IsType<FoodSource>(result.Carcass);
        Assert.Equal($"carcass-{firstCow.Id}", carcass.Id);
        Assert.Equal(Catalog.GetRequired("cow").CarcassMeatUnits,
            carcass.Contents.Counts[ItemIds.Meat]);
        Assert.False(world.TryGetEntity(firstCow.Id, out _));
        Assert.Equal(CreatureHarvestStatus.InvalidEntity,
            CreatureHarvestService.Harvest(world, firstCow, Catalog.GetRequired("cow").CarcassMeatUnits).Status);
        Assert.Equal(Catalog.GetRequired("cow").CarcassMeatUnits,
            carcass.Contents.Counts[ItemIds.Meat]);
        Assert.Single(world.FoodSources, source => source.Id == carcass.Id);
        Assert.Null(wolf.TargetId);
        Assert.Null(wolf.Actions.Current);

        var satietyBeforeEating = wolf.Satiety;
        var travelingToCarcass = feeding.Advance(wolf, Catalog.GetRequired("wolf"), simulationTick: 41);
        Assert.Equal(FoodConsumptionStatus.Traveling, travelingToCarcass.Status);
        world.TrySetEntityPosition(wolf.Id, carcass.Position);
        Assert.True(navigator.CompleteAsArrived(navigator.Requests.Single(pair =>
            pair.Value == NavigationOutcome.InProgress).Key));
        var ate = feeding.Advance(wolf, Catalog.GetRequired("wolf"), simulationTick: 42);
        Assert.Equal(FoodConsumptionStatus.Ate, ate.Status);
        Assert.True(wolf.Satiety > satietyBeforeEating);
        Assert.Equal(1, feeding.FoodUnitsConsumed);
        Assert.Equal(Catalog.GetRequired("cow").CarcassMeatUnits - 1,
            carcass.Contents.Counts[ItemIds.Meat]);
    }

    [Fact]
    public void HuntIgnoresDeadCapturedDomesticWrongSpeciesAndFriendlyTargets()
    {
        var world = new World();
        world.DefineFaction("wolf-home");
        var wolf = world.SpawnEntity(Catalog.GetRequired("wolf"), Vector3.Zero, satiety: 20);
        world.SetEntityAllegiance(wolf.Id, "wolf-home");
        var deadCow = world.SpawnEntity(Catalog.GetRequired("cow"), new Vector3(1, 0, 0));
        deadCow.Health.TryDamage("Torso", 1000);
        var capturedCow = world.SpawnEntity(Catalog.GetRequired("cow"), new Vector3(2, 0, 0));
        world.TryCaptureCreature(capturedCow.Id);
        var friendlyCow = world.SpawnEntity(Catalog.GetRequired("cow"), new Vector3(3, 0, 0));
        world.SetEntityAllegiance(friendlyCow.Id, "wolf-home");
        var wrongSpecies = world.SpawnEntity(Catalog.GetRequired("human"), new Vector3(0.5f, 0, 0));
        var domesticCow = world.SpawnEntity(Catalog.GetRequired("cow"), new Vector3(4, 0, 0));
        world.TryTransitionCreatureState(domesticCow.Id, CreatureCaptivityState.Captured);
        world.TryTransitionCreatureState(domesticCow.Id, CreatureCaptivityState.Captive);
        world.TryTransitionCreatureState(domesticCow.Id, CreatureCaptivityState.Domesticated);
        var navigator = new FakeNavigationAdapter();
        var hunting = new CreatureHuntingService(world, Catalog, _ => navigator);

        var result = hunting.Advance(wolf, simulationTick: 1);

        Assert.Equal(CreatureHuntingStatus.NoPrey, result.Status);
        Assert.Null(wolf.TargetId);
        Assert.Null(wolf.Actions.Current);
        Assert.True(world.TryGetEntity(deadCow.Id, out _));
        Assert.True(world.TryGetEntity(capturedCow.Id, out _));
        Assert.True(world.TryGetEntity(friendlyCow.Id, out _));
        Assert.True(world.TryGetEntity(wrongSpecies.Id, out _));
        Assert.True(world.TryGetEntity(domesticCow.Id, out _));
    }

    [Fact]
    public void UnreachablePreyClearsHuntAndTargetBeforeRetry()
    {
        var world = new World();
        var wolf = world.SpawnEntity(Catalog.GetRequired("wolf"), Vector3.Zero, satiety: 20);
        world.SpawnEntity(Catalog.GetRequired("cow"), new Vector3(5, 0, 0));
        var navigator = new FakeNavigationAdapter();
        var hunting = new CreatureHuntingService(world, Catalog, _ => navigator);

        var started = hunting.Advance(wolf, simulationTick: 1);
        Assert.Equal(CreatureHuntingStatus.Traveling, started.Status);
        Assert.NotNull(wolf.Actions.Current);
        Assert.NotNull(wolf.TargetId);
        var requestId = Assert.Single(navigator.Requests).Key;
        Assert.True(navigator.MarkUnreachable(requestId));

        var failed = hunting.Advance(wolf, simulationTick: 2);

        Assert.Equal(CreatureHuntingStatus.Waiting, failed.Status);
        Assert.False(hunting.IsHunting(wolf.Id));
        Assert.Null(wolf.TargetId);
        Assert.Null(wolf.Actions.Current);
        Assert.Equal("Waiting", wolf.Activity);
    }

    [Fact]
    public void HungryDisplacedWolfReturnsToTerritoryThenResumesHunting()
    {
        var world = new World();
        var wolf = world.SpawnEntity(Catalog.GetRequired("wolf"), new Vector3(10, 0, 0), satiety: 20);
        var cow = world.SpawnEntity(Catalog.GetRequired("cow"), new Vector3(1, 0, 0));
        world.SetWildTerritory(wolf.Id, Vector3.Zero, radius: 8);
        var navigator = new FakeNavigationAdapter();
        var hunting = new CreatureHuntingService(world, Catalog, _ => navigator);
        var wandering = new WanderingService(world, _ => navigator, [new Vector3(1, 0, 0)]);

        Assert.Equal(CreatureHuntingStatus.Waiting, hunting.Advance(wolf, simulationTick: 1).Status);
        var returning = wandering.Advance(wolf, Catalog.GetRequired("wolf"), simulationTick: 1);
        Assert.Equal(WanderingStatus.ReturningToTerritory, returning.Status);
        var returnRequest = Assert.Single(navigator.Requests).Key;

        Assert.Equal(CreatureHuntingStatus.Waiting, hunting.Advance(wolf, simulationTick: 2).Status);
        Assert.Equal(WanderingStatus.ReturningToTerritory,
            wandering.Advance(wolf, Catalog.GetRequired("wolf"), simulationTick: 2).Status);
        Assert.Equal(NavigationOutcome.InProgress, navigator.Requests[returnRequest]);

        Assert.True(navigator.CompleteAsArrived(returnRequest));
        world.TrySetEntityPosition(wolf.Id, Vector3.Zero);
        Assert.Equal(WanderingStatus.NeedsFood,
            wandering.Advance(wolf, Catalog.GetRequired("wolf"), simulationTick: 3).Status);
        Assert.Equal(CreatureHuntingStatus.Attacked, hunting.Advance(wolf, simulationTick: 3).Status);
        Assert.Equal(cow.Id, wolf.TargetId);
    }
}
