using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class WanderingServiceTests
{
    [Fact]
    public void ThreeCreaturesUseDistinctStableIdsAndSharedWanderingAction()
    {
        var world = new World();
        var species = GetCreatureSpecies();
        var navigator = new FakeNavigationAdapter();
        var service = new WanderingService(world, _ => navigator, [new Vector3(2, 0, 0)]);

        var creatures = Enumerable.Range(0, 3)
            .Select(index => world.SpawnEntity(species.Id, new Vector3(index, 0, 0)))
            .ToArray();

        var results = creatures.Select(entity => service.Advance(entity, species)).ToArray();

        Assert.Equal(new[] { "entity-000001", "entity-000002", "entity-000003" },
            creatures.Select(entity => entity.Id));
        Assert.All(results, result => Assert.Equal(WanderingStatus.Traveling, result.Status));
        Assert.All(creatures, entity => Assert.Equal(ActionKind.Wander, entity.Actions.Current?.Kind));
        Assert.All(creatures, entity => Assert.Equal("Wandering", entity.Activity));
        Assert.Equal(3, navigator.Requests.Count);
    }

    [Fact]
    public void CreatureReachesBoundedGoalAndStartsNextGoalAfterArrival()
    {
        var world = new World();
        var species = GetCreatureSpecies();
        var navigator = new FakeNavigationAdapter();
        var goals = new[] { new Vector3(2, 0, 0), new Vector3(-2, 0, 0) };
        var service = new WanderingService(world, _ => navigator, goals);
        var creature = world.SpawnEntity(species.Id, Vector3.Zero, satiety: 31);

        var started = service.Advance(creature, species);
        var firstRequest = navigator.Requests.Keys.Single();
        Assert.Equal(WanderingStatus.Traveling, started.Status);
        Assert.Equal(goals[0], started.Target);

        Assert.True(navigator.CompleteAsArrived(firstRequest));
        world.TrySetEntityPosition(creature.Id, goals[0]);
        var arrived = service.Advance(creature, species);

        Assert.Equal(WanderingStatus.Arrived, arrived.Status);
        Assert.Null(creature.Actions.Current);
        Assert.Equal("Idle", creature.Activity);

        var next = service.Advance(creature, species);
        Assert.Equal(WanderingStatus.Traveling, next.Status);
        Assert.Equal(goals[1], next.Target);
    }

    [Fact]
    public void HungryCreatureDoesNotWanderAndCancelsItsActiveTrip()
    {
        var world = new World(new NeedsTuning(initialSatiety: 31, drainPerSecond: 1));
        var species = GetCreatureSpecies();
        var navigator = new FakeNavigationAdapter();
        var service = new WanderingService(world, _ => navigator, [new Vector3(2, 0, 0)]);
        var creature = world.SpawnEntity(species.Id, Vector3.Zero, satiety: 31);

        Assert.Equal(WanderingStatus.Traveling, service.Advance(creature, species).Status);
        creature.Needs.Advance(1);

        var waiting = service.Advance(creature, species);

        Assert.Equal(WanderingStatus.NeedsFood, waiting.Status);
        Assert.Null(creature.Actions.Current);
        Assert.Equal(NavigationOutcome.Cancelled, navigator.Requests.Values.Single());
    }

    [Fact]
    public void HumanCannotUseCreatureWanderingService()
    {
        var world = new World();
        var human = SpeciesCatalog.LoadFromFile(GetSpeciesDataPath()).GetRequired("human");
        var navigator = new FakeNavigationAdapter();
        var service = new WanderingService(world, _ => navigator, [Vector3.One]);
        var entity = world.SpawnEntity(human.Id, Vector3.Zero);

        var result = service.Advance(entity, human);

        Assert.Equal(WanderingStatus.NotWanderer, result.Status);
        Assert.Empty(navigator.Requests);
        Assert.Null(entity.Actions.Current);
    }

    private static SpeciesDefinition GetCreatureSpecies()
    {
        return SpeciesCatalog.LoadFromFile(GetSpeciesDataPath()).GetRequired("creature");
    }

    private static string GetSpeciesDataPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "data", "species.json");
    }
}
