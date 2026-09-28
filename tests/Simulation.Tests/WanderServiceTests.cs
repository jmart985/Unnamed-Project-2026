using System.Numerics;
using System.Text;
using Simulation;

namespace Simulation.Tests;

public sealed class WanderServiceTests
{
    [Fact]
    public void FedCreatureStartsWanderingToBoundedGoalsAndCompletesOnArrival()
    {
        var world = new World();
        var creature = world.SpawnEntity("creature", Vector3.Zero);
        var navigator = new FakeNavigationAdapter();
        var service = new WanderService(world, _ => navigator);
        var species = LoadSpecies(
            """[{"id":"creature","movementSpeed":2,"hungerRate":0.2,"diet":["food"],"canHaul":false}]""",
            "creature");
        var goals = new[] { new Vector3(2, 0, 0), new Vector3(4, 0, 0) };

        var started = service.Advance(creature, species, goals);

        Assert.Equal(WanderStatus.Traveling, started.Status);
        Assert.Equal("Wandering", creature.Activity);
        Assert.Equal(goals[0], started.Goal);
        Assert.Single(navigator.Requests);

        Assert.True(navigator.CompleteAsArrived(navigator.Requests.Keys.Single()));
        var arrived = service.Advance(creature, species, goals);

        Assert.Equal(WanderStatus.Arrived, arrived.Status);
        Assert.Null(creature.Actions.Current);
        Assert.Equal("Idle", creature.Activity);

        var restarted = service.Advance(creature, species, goals);
        Assert.Equal(WanderStatus.Traveling, restarted.Status);
        Assert.Equal(goals[1], restarted.Goal);
    }

    [Fact]
    public void HungryCreatureDoesNotStartWandering()
    {
        var world = new World();
        var creature = world.SpawnEntity("creature", Vector3.Zero, satiety: 20);
        var navigator = new FakeNavigationAdapter();
        var service = new WanderService(world, _ => navigator);
        var species = LoadSpecies(
            """[{"id":"creature","movementSpeed":2,"hungerRate":0.2,"diet":["food"],"canHaul":false}]""",
            "creature");

        var result = service.Advance(creature, species, [new Vector3(2, 0, 0)]);

        Assert.Equal(WanderStatus.Hungry, result.Status);
        Assert.Empty(navigator.Requests);
        Assert.Null(creature.Actions.Current);
    }

    [Fact]
    public void FailedWanderCancelsTheActionInsteadOfCompletingIt()
    {
        var world = new World();
        var creature = world.SpawnEntity("creature", Vector3.Zero);
        var navigator = new FakeNavigationAdapter();
        var service = new WanderService(world, _ => navigator);
        var species = LoadSpecies(
            """[{"id":"creature","movementSpeed":2,"hungerRate":0.2,"diet":["food"],"canHaul":false}]""",
            "creature");

        var started = service.Advance(creature, species, [new Vector3(2, 0, 0)]);
        var requestId = navigator.Requests.Keys.Single();

        Assert.Equal(WanderStatus.Traveling, started.Status);
        Assert.True(navigator.MarkUnreachable(requestId));

        var failed = service.Advance(creature, species, [new Vector3(2, 0, 0)]);

        Assert.Equal(WanderStatus.ActionRejected, failed.Status);
        Assert.Null(creature.Actions.Current);
        Assert.Equal("Idle", creature.Activity);
        Assert.Equal(NavigationOutcome.Unreachable, navigator.Poll(requestId, 0));
    }

    [Fact]
    public void SpeciesHungerRateDrivesSatietyLossWithoutSpeciesSpecificLogic()
    {
        var catalog = LoadCatalog(
            """
            [
              {"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true},
              {"id":"creature","movementSpeed":2,"hungerRate":0.5,"diet":["food"],"canHaul":false}
            ]
            """);
        var world = new World();
        var human = world.SpawnEntity("human", Vector3.Zero);
        var creature = world.SpawnEntity("creature", Vector3.Zero);

        human.Needs.Advance(10, catalog.GetRequired(human.SpeciesId).HungerRate);
        creature.Needs.Advance(10, catalog.GetRequired(creature.SpeciesId).HungerRate);

        Assert.Equal(58, human.Satiety, precision: 6);
        Assert.Equal(55, creature.Satiety, precision: 6);
    }

    private static SpeciesCatalog LoadCatalog(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return SpeciesCatalog.Load(stream);
    }

    private static SpeciesDefinition LoadSpecies(string json, string speciesId)
    {
        return LoadCatalog(json).GetRequired(speciesId);
    }
}
