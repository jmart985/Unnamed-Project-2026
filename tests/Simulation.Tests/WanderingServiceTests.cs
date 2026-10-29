using System.Numerics;
using System.Text;
using Simulation;

namespace Simulation.Tests;

public sealed class WanderingServiceTests
{
    [Fact]
    public void TemperamentSelectsDistinctRepeatableWildBehaviors()
    {
        var catalog = SpeciesCatalog.LoadFromFile(GetSpeciesDataPath());
        var goals = new[] { new Vector3(1, 0, 0), new Vector3(3, 0, 0), new Vector3(5, 0, 0) };

        var cautious = StartWildTrip(catalog.GetRequired("creature"), goals, seed: 42, tick: 7);
        var exploratory = StartWildTrip(catalog.GetRequired("wolf"), goals, seed: 42, tick: 7);
        var repeatedExploratory = StartWildTrip(catalog.GetRequired("wolf"), goals, seed: 42, tick: 7);

        Assert.Equal(WildBehavior.CautiousRoaming, cautious.Behavior);
        Assert.Equal(goals[0], cautious.Target);
        Assert.Equal(WildBehavior.CuriousExploration, exploratory.Behavior);
        Assert.Equal(goals[2], exploratory.Target);
        Assert.Equal(exploratory, repeatedExploratory);
    }

    [Fact]
    public void FailedWanderReleasesActionAndAllowsNextTrip()
    {
        var world = new World();
        var species = GetCreatureSpecies();
        var navigator = new FakeNavigationAdapter();
        var goals = new[] { new Vector3(1, 0, 0), new Vector3(3, 0, 0) };
        var service = new WanderingService(world, _ => navigator, goals, decisionSeed: 42);
        var creature = world.SpawnEntity(species.Id, Vector3.Zero);

        var started = service.Advance(creature, species, simulationTick: 1);
        var requestId = navigator.Requests.Keys.Single();
        Assert.True(navigator.MarkUnreachable(requestId));

        var failed = service.Advance(creature, species, simulationTick: 2);

        Assert.Equal(WanderingStatus.Waiting, failed.Status);
        Assert.Equal("Wandering target failed: Unreachable.", failed.WaitingReason);
        Assert.Equal(WildBehavior.CautiousRoaming, failed.Behavior);
        Assert.Null(creature.Actions.Current);

        var restarted = service.Advance(creature, species, simulationTick: 3);
        Assert.Equal(WanderingStatus.Traveling, restarted.Status);
        Assert.NotNull(creature.Actions.Current);
        Assert.Null(creature.WaitingReason);
    }

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
        var world = new World(new NeedsTuning(initialSatiety: 31, satietyDrainPerSecond: 1));
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
    public void ReassignedActionCancelsNavigationAndAllowsWanderingToResumeLater()
    {
        var world = new World();
        var species = GetCreatureSpecies();
        var navigator = new FakeNavigationAdapter();
        var service = new WanderingService(world, _ => navigator, [new Vector3(2, 0, 0)]);
        var creature = world.SpawnEntity(species.Id, Vector3.Zero);

        var started = service.Advance(creature, species);
        var requestId = navigator.Requests.Keys.Single();
        var replaced = creature.Actions.TryStart(new ActionCandidate("eat-creature", ActionKind.Eat));

        var blocked = service.Advance(creature, species);

        Assert.Equal(ActionStartResult.Preempted, replaced);
        Assert.Equal(WanderingStatus.ActionRejected, blocked.Status);
        Assert.Equal("eat-creature", creature.Actions.Current?.Id);
        Assert.Equal(NavigationOutcome.Cancelled, navigator.Poll(requestId, 0));

        creature.Actions.Cancel();
        var resumed = service.Advance(creature, species);

        Assert.Equal(WanderingStatus.Traveling, resumed.Status);
        Assert.NotEqual(requestId, navigator.Requests.Keys.Last());
        Assert.Equal(ActionKind.Wander, creature.Actions.Current?.Kind);
    }

    [Fact]
    public void ResetCancelsActiveNavigationAndClearsDeterministicTripState()
    {
        var world = new World();
        var species = GetCreatureSpecies();
        var navigator = new FakeNavigationAdapter();
        var goals = new[] { new Vector3(2, 0, 0), new Vector3(-2, 0, 0) };
        var service = new WanderingService(world, _ => navigator, goals, decisionSeed: 42);
        var creature = world.SpawnEntity(species.Id, Vector3.Zero);

        var firstTrip = service.Advance(creature, species, simulationTick: 7);
        var firstRequestId = navigator.Requests.Keys.Single();
        service.Reset();

        Assert.Equal(NavigationOutcome.Cancelled, navigator.Poll(firstRequestId, 0));
        Assert.Null(creature.Actions.Current);

        var afterReset = service.Advance(creature, species, simulationTick: 7);

        Assert.Equal(WanderingStatus.Traveling, afterReset.Status);
        Assert.Equal(firstTrip.Target, afterReset.Target);
        Assert.Equal(ActionKind.Wander, creature.Actions.Current?.Kind);
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

    [Fact]
    public void HumanSpeciesCanOptIntoTerritoryBehaviorThroughData()
    {
        var species = LoadHumanSpecies(usesWildTerritory: true);
        var world = new World();
        var human = world.SpawnEntity(species.Id, new Vector3(10, 0, 0));
        world.SetWildTerritory(human.Id, Vector3.Zero, radius: 8);
        var navigator = new FakeNavigationAdapter();
        var service = new WanderingService(world, _ => navigator, [new Vector3(100, 0, 100)]);

        var result = service.Advance(human, species, simulationTick: 1);

        Assert.Equal(WanderingStatus.ReturningToTerritory, result.Status);
        Assert.Equal(Vector3.Zero, result.Target);
        Assert.Single(navigator.RequestTargets.Values, target => target == Vector3.Zero);
    }

    [Fact]
    public void HumanSpeciesCanOptOutOfTerritoryBehaviorThroughData()
    {
        var species = LoadHumanSpecies(usesWildTerritory: false);
        var world = new World();
        var human = world.SpawnEntity(species.Id, new Vector3(10, 0, 0));
        world.SetWildTerritory(human.Id, Vector3.Zero, radius: 8);
        var navigator = new FakeNavigationAdapter();
        var service = new WanderingService(world, _ => navigator, [new Vector3(2, 0, 0)]);

        var result = service.Advance(human, species, simulationTick: 1);

        Assert.Equal(WanderingStatus.Traveling, result.Status);
        Assert.Equal(new Vector3(2, 0, 0), result.Target);
        Assert.DoesNotContain(navigator.RequestTargets.Values, target => target == Vector3.Zero);
    }

    [Fact]
    public void WildRoamingStaysInsideAssignedTerritoryAndReturnsWhenDisplaced()
    {
        var world = new World();
        var species = SpeciesCatalog.LoadFromFile(GetSpeciesDataPath()).GetRequired("wolf");
        var wolf = world.SpawnEntity(species.Id, Vector3.Zero);
        var territory = new WildTerritory(wolf.Id, Vector3.Zero, 8);
        Assert.Equal(WildTerritoryResult.Accepted,
            world.SetWildTerritory(wolf.Id, territory.Center, territory.Radius));
        var navigator = new FakeNavigationAdapter();
        var service = new WanderingService(world, _ => navigator, [new Vector3(100, 0, 100)]);

        var roaming = service.Advance(wolf, species, simulationTick: 1);

        Assert.Equal(WanderingStatus.Traveling, roaming.Status);
        Assert.NotNull(roaming.Target);
        Assert.True(territory.Contains(roaming.Target!.Value));

        var roamRequest = navigator.Requests.Keys.Single();
        world.TrySetEntityPosition(wolf.Id, new Vector3(10, 0, 0));
        var returning = service.Advance(wolf, species, simulationTick: 2);

        Assert.Equal(NavigationOutcome.Cancelled, navigator.Requests[roamRequest]);
        Assert.Equal(WanderingStatus.ReturningToTerritory, returning.Status);
        Assert.Equal(territory.Center, returning.Target);
        Assert.Equal(ActionKind.Wander, wolf.Actions.Current?.Kind);
        Assert.Equal(territory.Center, navigator.RequestTargets.Values.Last());

        var returnRequest = navigator.Requests.Keys.Last();
        Assert.True(navigator.CompleteAsArrived(returnRequest));
        world.TrySetEntityPosition(wolf.Id, territory.Center);
        var arrived = service.Advance(wolf, species, simulationTick: 3);

        Assert.Equal(WanderingStatus.ReturnedToTerritory, arrived.Status);
        Assert.Null(wolf.Actions.Current);
        var resumedRoam = service.Advance(wolf, species, simulationTick: 4);
        Assert.Equal(WanderingStatus.Traveling, resumedRoam.Status);
        Assert.True(territory.Contains(resumedRoam.Target!.Value));
    }

    [Fact]
    public void HungryDisplacedWildCreatureReturnsHomeBeforeSeekingFood()
    {
        var world = new World(new NeedsTuning(satietyDrainPerSecond: 2));
        var species = SpeciesCatalog.LoadFromFile(GetSpeciesDataPath()).GetRequired("wolf");
        var wolf = world.SpawnEntity(species.Id, new Vector3(10, 0, 0), satiety: 31);
        world.SetWildTerritory(wolf.Id, Vector3.Zero, 8);
        var navigator = new FakeNavigationAdapter();
        var service = new WanderingService(world, _ => navigator, [Vector3.One]);

        Assert.Equal(WanderingStatus.ReturningToTerritory,
            service.Advance(wolf, species, simulationTick: 1).Status);
        var returnRequest = navigator.Requests.Keys.Single();
        wolf.Needs.Advance(1);

        Assert.Equal(WanderingStatus.ReturningToTerritory,
            service.Advance(wolf, species, simulationTick: 2).Status);
        Assert.Equal(NavigationOutcome.InProgress, navigator.Requests[returnRequest]);
        Assert.Equal(ActionKind.Wander, wolf.Actions.Current?.Kind);

        Assert.True(navigator.CompleteAsArrived(returnRequest));
        world.TrySetEntityPosition(wolf.Id, Vector3.Zero);
        Assert.Equal(WanderingStatus.NeedsFood, service.Advance(wolf, species, simulationTick: 3).Status);
        Assert.Null(wolf.Actions.Current);

        wolf.Needs.AddNutrition(50);
        Assert.Equal(WanderingStatus.Traveling, service.Advance(wolf, species, simulationTick: 4).Status);
        Assert.Equal(2, navigator.Requests.Count);
    }

    [Fact]
    public void CapturedCreatureDoesNotReceiveForcedTerritoryReturn()
    {
        var world = new World();
        var catalog = SpeciesCatalog.LoadFromFile(GetSpeciesDataPath());
        world.SpeciesCatalog = catalog;
        var species = catalog.GetRequired("wolf");
        var wolf = world.SpawnEntity(species.Id, new Vector3(10, 0, 0));
        world.SetWildTerritory(wolf.Id, Vector3.Zero, 8);
        Assert.Equal(CreatureCaptureResult.Captured, world.TryCaptureCreature(wolf.Id));
        var navigator = new FakeNavigationAdapter();
        var service = new WanderingService(world, _ => navigator, [new Vector3(2, 0, 0)]);

        var result = service.Advance(wolf, species, simulationTick: 1);

        Assert.Equal(WanderingStatus.Traveling, result.Status);
        Assert.Equal(new Vector3(2, 0, 0), result.Target);
        Assert.NotEqual(Vector3.Zero, result.Target);
    }

    [Fact]
    public void RepeatedTerritoryRouteFailuresDeferRetryAndClearAction()
    {
        var world = new World();
        var species = SpeciesCatalog.LoadFromFile(GetSpeciesDataPath()).GetRequired("wolf");
        var wolf = world.SpawnEntity(species.Id, new Vector3(10, 0, 0));
        world.SetWildTerritory(wolf.Id, Vector3.Zero, 8);
        var navigator = new FakeNavigationAdapter();
        var service = new WanderingService(world, _ => navigator, [Vector3.One]);

        Assert.Equal(WanderingStatus.ReturningToTerritory,
            service.Advance(wolf, species, simulationTick: 1).Status);
        var firstRequest = navigator.Requests.Keys.Single();
        Assert.True(navigator.MarkUnreachable(firstRequest));
        var failed = service.Advance(wolf, species, simulationTick: 2);
        Assert.Equal(WanderingStatus.Waiting, failed.Status);
        Assert.Contains("retry deferred", failed.WaitingReason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(wolf.Actions.Current);
        Assert.Equal(WanderingStatus.Waiting, service.Advance(wolf, species, simulationTick: 11).Status);
        Assert.Single(navigator.Requests);

        Assert.Equal(WanderingStatus.ReturningToTerritory,
            service.Advance(wolf, species, simulationTick: 12).Status);
        Assert.Equal(2, navigator.Requests.Count);
    }

    private static SpeciesDefinition GetCreatureSpecies()
    {
        return SpeciesCatalog.LoadFromFile(GetSpeciesDataPath()).GetRequired("creature");
    }

    private static SpeciesDefinition LoadHumanSpecies(bool usesWildTerritory)
    {
        var flag = usesWildTerritory ? "true" : "false";
        var json = $$$"""
            [{"id":"human","bodyCategory":"humanoid","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":false,"usesWildTerritory":{{{flag}}},"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}]
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return SpeciesCatalog.Load(stream).GetRequired("human");
    }

    private static string GetSpeciesDataPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "data", "species.json");
    }

    private static WanderingResult StartWildTrip(
        SpeciesDefinition species,
        IReadOnlyList<Vector3> goals,
        int seed,
        long tick)
    {
        var world = new World();
        var entity = world.SpawnEntity(species.Id, Vector3.Zero);
        var service = new WanderingService(world, _ => new FakeNavigationAdapter(), goals, seed);
        return service.Advance(entity, species, tick);
    }
}
