using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class WildPreyThreatServiceTests
{
    private static readonly SpeciesCatalog Catalog = SpeciesCatalog.LoadFromFile(
        Path.Combine(AppContext.BaseDirectory, "data", "species.json"));

    [Fact]
    public void WildPreyFleesFromNearestEligiblePredatorAndStopsWhenItLeavesRange()
    {
        var world = new World();
        var cow = world.SpawnEntity(Catalog.GetRequired("cow"), Vector3.Zero, satiety: 0);
        var nearestWolf = world.SpawnEntity(Catalog.GetRequired("wolf"), new Vector3(-4, 0, 0));
        var fartherWolf = world.SpawnEntity(Catalog.GetRequired("wolf"), new Vector3(8, 0, 0));
        var navigator = new FakeNavigationAdapter();
        var service = new WildPreyThreatService(world, Catalog, _ => navigator);

        var fleeing = service.Advance(cow, simulationTick: 1);

        Assert.Equal(WildPreyThreatStatus.Fleeing, fleeing.Status);
        Assert.Equal(nearestWolf.Id, fleeing.ThreatEntityId);
        Assert.Equal(new Vector3(10, 0, 0), fleeing.FleeTarget);
        Assert.Equal(ActionKind.Flee, cow.Actions.Current!.Kind);
        Assert.True(cow.Actions.Current.Priority > new ActionCandidate("eat", ActionKind.Eat).Priority);
        Assert.Equal(nearestWolf.Id, cow.TargetId);
        Assert.True(service.SuppressesOtherBehavior(cow.Id));

        world.TrySetEntityPosition(nearestWolf.Id, new Vector3(-30, 0, 0));
        world.TrySetEntityPosition(fartherWolf.Id, new Vector3(30, 0, 0));
        var safe = service.Advance(cow, simulationTick: 2);

        Assert.Equal(WildPreyThreatStatus.NoThreat, safe.Status);
        Assert.Null(cow.Actions.Current);
        Assert.Null(cow.TargetId);
        Assert.False(service.IsFleeing(cow.Id));
        Assert.False(service.SuppressesOtherBehavior(cow.Id));
        Assert.Equal(NavigationOutcome.Cancelled, navigator.Requests.Values.First());
    }

    [Fact]
    public void ThreatRangeIncludesBoundaryAndTargetTieBreakUsesStableEntityId()
    {
        var world = new World();
        var cow = world.SpawnEntity(Catalog.GetRequired("cow"), Vector3.Zero);
        var firstWolf = world.SpawnEntity(Catalog.GetRequired("wolf"), new Vector3(0, 0, 18));
        world.SpawnEntity(Catalog.GetRequired("wolf"), new Vector3(0, 0, -18));
        var navigator = new FakeNavigationAdapter();
        var service = new WildPreyThreatService(world, Catalog, _ => navigator);

        var atBoundary = service.Advance(cow, simulationTick: 1);

        Assert.Equal(WildPreyThreatStatus.Fleeing, atBoundary.Status);
        Assert.Equal(firstWolf.Id, atBoundary.ThreatEntityId);
        Assert.Equal(new Vector3(0, 0, -10), atBoundary.FleeTarget);
    }

    [Fact]
    public void DeadIncapacitatedFriendlyAndUnrelatedEntitiesDoNotTriggerFleeing()
    {
        var world = new World();
        world.DefineFaction("herd");
        var cow = world.SpawnEntity(Catalog.GetRequired("cow"), Vector3.Zero);
        world.SetEntityAllegiance(cow.Id, "herd");
        var friendlyWolf = world.SpawnEntity(Catalog.GetRequired("wolf"), new Vector3(1, 0, 0));
        world.SetEntityAllegiance(friendlyWolf.Id, "herd");
        var deadWolf = world.SpawnEntity(Catalog.GetRequired("wolf"), new Vector3(2, 0, 0));
        deadWolf.Health.TryDamage("Torso", 1000);
        var incapacitatedWolf = world.SpawnEntity(Catalog.GetRequired("wolf"), new Vector3(3, 0, 0));
        incapacitatedWolf.Health.TryDamage("Head", 1000);
        world.SpawnEntity(Catalog.GetRequired("ape"), new Vector3(0, 0, 0.5f));
        var navigator = new FakeNavigationAdapter();
        var service = new WildPreyThreatService(world, Catalog, _ => navigator);

        var result = service.Advance(cow, simulationTick: 1);

        Assert.Equal(WildPreyThreatStatus.NoThreat, result.Status);
        Assert.Null(cow.Actions.Current);
        Assert.Empty(navigator.Requests);

        world.TryCaptureCreature(cow.Id);
        Assert.Equal(WildPreyThreatStatus.NotEligible, service.Advance(cow, simulationTick: 2).Status);
    }

    [Fact]
    public void FailedEscapeRouteClearsActionAndRetriesAfterBoundedDelay()
    {
        var world = new World();
        var cow = world.SpawnEntity(Catalog.GetRequired("cow"), Vector3.Zero);
        world.SpawnEntity(Catalog.GetRequired("wolf"), new Vector3(-4, 0, 0));
        var navigator = new FakeNavigationAdapter();
        var service = new WildPreyThreatService(world, Catalog, _ => navigator);

        Assert.Equal(WildPreyThreatStatus.Fleeing, service.Advance(cow, 1).Status);
        var requestId = Assert.Single(navigator.Requests).Key;
        Assert.True(navigator.MarkUnreachable(requestId));

        Assert.Equal(WildPreyThreatStatus.Waiting, service.Advance(cow, 2).Status);
        Assert.Null(cow.Actions.Current);
        Assert.False(service.IsFleeing(cow.Id));
        Assert.Equal(WildPreyThreatStatus.Waiting, service.Advance(cow, 11).Status);
        Assert.Single(navigator.Requests);

        Assert.Equal(WildPreyThreatStatus.Fleeing, service.Advance(cow, 12).Status);
        Assert.Equal(2, navigator.Requests.Count);
    }

    [Fact]
    public void FailedEscapeWaitClearsWhenThreatLeavesRange()
    {
        var world = new World();
        var cow = world.SpawnEntity(Catalog.GetRequired("cow"), Vector3.Zero);
        var wolf = world.SpawnEntity(Catalog.GetRequired("wolf"), new Vector3(-4, 0, 0));
        var navigator = new FakeNavigationAdapter();
        var service = new WildPreyThreatService(world, Catalog, _ => navigator);
        service.Advance(cow, simulationTick: 1);
        Assert.True(navigator.MarkUnreachable(Assert.Single(navigator.Requests).Key));
        Assert.Equal(WildPreyThreatStatus.Waiting, service.Advance(cow, simulationTick: 2).Status);

        world.TrySetEntityPosition(wolf.Id, new Vector3(-40, 0, 0));
        Assert.Equal(WildPreyThreatStatus.NoThreat, service.Advance(cow, simulationTick: 3).Status);

        Assert.Equal("Idle", cow.Activity);
        Assert.Null(cow.WaitingReason);
        Assert.False(service.SuppressesOtherBehavior(cow.Id));
    }

    [Fact]
    public void NoConfiguredDetectionRangeKeepsLegacySpeciesUnaffected()
    {
        var world = new World();
        var ape = world.SpawnEntity(Catalog.GetRequired("ape"), Vector3.Zero);
        world.SpawnEntity(Catalog.GetRequired("wolf"), Vector3.One);
        var navigator = new FakeNavigationAdapter();
        var service = new WildPreyThreatService(world, Catalog, _ => navigator);

        var result = service.Advance(ape, simulationTick: 1);

        Assert.Equal(WildPreyThreatStatus.NotEligible, result.Status);
        Assert.Empty(navigator.Requests);
    }

    [Fact]
    public void RemovedPreyReleasesItsTransientEscapeActionAndNavigation()
    {
        var world = new World();
        var cow = world.SpawnEntity(Catalog.GetRequired("cow"), Vector3.Zero);
        world.SpawnEntity(Catalog.GetRequired("wolf"), new Vector3(-4, 0, 0));
        var navigator = new FakeNavigationAdapter();
        var service = new WildPreyThreatService(world, Catalog, _ => navigator);
        service.Advance(cow, simulationTick: 1);

        Assert.True(world.TryRemoveEntity(cow.Id));
        Assert.Equal(WildPreyThreatStatus.NotEligible, service.Advance(cow, simulationTick: 2).Status);

        Assert.Null(cow.Actions.Current);
        Assert.Null(cow.TargetId);
        Assert.False(service.IsFleeing(cow.Id));
        Assert.False(service.SuppressesOtherBehavior(cow.Id));
        Assert.Equal(NavigationOutcome.Cancelled, navigator.Requests.Values.Single());
    }
}
