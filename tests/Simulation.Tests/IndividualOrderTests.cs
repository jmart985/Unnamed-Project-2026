using System.Numerics;
using System.Text.Json.Nodes;
using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class IndividualOrderTests
{
    private static SpeciesCatalog Catalog() => SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));

    private static (World World, SpeciesCatalog Catalog, EntityRecord Player, EntityRecord Target, InteractionResolver Resolver) Harness()
    {
        var catalog = Catalog();
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Ada"), seed: 42);
        var target = session.World.Entities.First(entity => entity.SpeciesId == "human" && entity.Id != session.World.PlayerEntityId);
        var resolver = new InteractionResolver(
            session.World,
            TradeGoodsCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "trade_goods.json")),
            new RelationshipEventService(session.World,
                RelationshipEventCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "relationship_events.json"))));
        return (session.World, catalog, session.Player, target, resolver);
    }

    private static void Bond(World world, EntityRecord actor, EntityRecord target)
        => world.ApplyRelationshipAdjustment(target.Id, actor.Id, new RelationshipAdjustment(0, 0, 0, 80));

    [Fact]
    public void FollowUsesIssuerPositionAndTracksMovementThroughNavigation()
    {
        var (world, _, _, target, resolver) = Harness();
        var issuer = world.SpawnEntity("human", Vector3.Zero);
        Bond(world, issuer, target);
        world.TrySetEntityPosition(issuer.Id, Vector3.Zero);
        world.TrySetEntityPosition(target.Id, new Vector3(8, 0, 0));
        var navigators = world.Entities.Where(entity => entity.Id != world.PlayerEntityId)
            .ToDictionary(entity => entity.Id, _ => new FakeNavigationAdapter(), StringComparer.Ordinal);
        var orders = new OrderService(world, entity => navigators[entity.Id]);

        var result = resolver.Resolve(new InteractionRequest(issuer.Id, target.Id, InteractionType.Command, "follow"));
        Assert.True(result.Succeeded);
        Assert.Equal(new EntityOrder(EntityOrderKind.Follow, issuer.Id), target.OrderIntent);
        orders.Advance();

        var nav = navigators[target.Id];
        var firstRequest = Assert.Single(nav.RequestTargets);
        Assert.InRange(firstRequest.Value.X, 1.49f, 1.51f);
        Assert.Equal(ActionKind.CommandMove, target.Actions.Current?.Kind);

        world.TrySetEntityPosition(issuer.Id, new Vector3(12, 0, 0));
        orders.Advance();
        Assert.Equal(NavigationOutcome.Cancelled, nav.Requests[firstRequest.Key]);
        Assert.Equal(2, nav.RequestTargets.Count);
        var secondRequest = nav.RequestTargets.Last();
        Assert.InRange(secondRequest.Value.X, 10.49f, 10.51f);

        var clipped = orders.ConstrainFollowStep(target.Id, new Vector3(5, 0, 0), new Vector3(15, 0, 0));
        Assert.InRange(Vector3.Distance(clipped, issuer.Position), 1.999f, 2.001f);

        world.TrySetEntityPosition(target.Id, new Vector3(10, 0, 0));
        orders.Advance();
        world.TrySetEntityPosition(issuer.Id, new Vector3(12.5f, 0, 0));
        orders.Advance();
        Assert.Equal(2, nav.RequestTargets.Count); // The three-metre resume band prevents route churn.
        world.TrySetEntityPosition(issuer.Id, new Vector3(13.1f, 0, 0));
        orders.Advance();
        Assert.Equal(3, nav.RequestTargets.Count);
    }

    [Fact]
    public void StayRemembersIssuePositionAndHoldsAfterArrival()
    {
        var (world, _, player, target, resolver) = Harness();
        Bond(world, player, target);
        var issuedAt = new Vector3(4, 0, 7);
        world.TrySetEntityPosition(target.Id, issuedAt);
        var nav = new FakeNavigationAdapter();
        var orders = new OrderService(world, _ => nav);

        Assert.True(resolver.Resolve(new InteractionRequest(player.Id, target.Id, InteractionType.Command, "stay")).Succeeded);
        Assert.Equal(new EntityOrder(EntityOrderKind.Stay, player.Id, issuedAt), target.OrderIntent);
        orders.Advance();
        Assert.Equal(ActionKind.CommandMove, target.Actions.Current?.Kind);
        Assert.Empty(nav.Requests);

        world.TrySetEntityPosition(target.Id, new Vector3(12, 0, 7));
        orders.Advance();
        var request = Assert.Single(nav.RequestTargets);
        Assert.Equal(issuedAt, request.Value);
        world.TrySetEntityPosition(target.Id, issuedAt);
        orders.Advance();
        Assert.Equal(NavigationOutcome.Cancelled, nav.Requests[request.Key]);
        Assert.Equal("Order: staying", target.Activity);
        Assert.Equal(ActionKind.CommandMove, target.Actions.Current?.Kind);
        orders.Advance();
        Assert.Single(nav.Requests);
    }

    [Fact]
    public void HungerTemporarilyInterruptsOrderAndOrderResumesAfterEating()
    {
        var (world, _, player, target, resolver) = Harness();
        Bond(world, player, target);
        world.TrySetEntityPosition(target.Id, new Vector3(9, 0, 0));
        var nav = new FakeNavigationAdapter();
        var orders = new OrderService(world, _ => nav);
        Assert.True(resolver.Resolve(new InteractionRequest(player.Id, target.Id, InteractionType.Command, "stay")).Succeeded);
        world.TrySetEntityPosition(target.Id, new Vector3(12, 0, 0));
        orders.Advance();
        var originalRequest = Assert.Single(nav.Requests).Key;

        target.Needs.Advance(3_000_000);
        Assert.True(target.Needs.IsHungry);
        Assert.Equal(ActionStartResult.Preempted, target.Actions.TryStart(new ActionCandidate("eat-test", ActionKind.Eat)));
        orders.Advance();
        Assert.Equal(NavigationOutcome.Cancelled, nav.Requests[originalRequest]);
        Assert.NotNull(target.OrderIntent);
        Assert.False(orders.ShouldSuppressAutonomousBehavior(target));

        target.Needs.AddNutrition(100);
        target.Actions.Complete();
        orders.Advance();
        Assert.Equal(2, nav.Requests.Count);
        Assert.Equal(ActionKind.CommandMove, target.Actions.Current?.Kind);
        Assert.NotNull(target.OrderIntent);
    }

    [Fact]
    public void AttentionActionPausesIndividualMovementAndRebuildsItsRouteWhenComplete()
    {
        var (world, _, player, target, resolver) = Harness();
        Bond(world, player, target);
        var stayPosition = new Vector3(4, 0, 7);
        world.TrySetEntityPosition(target.Id, stayPosition);
        Assert.True(resolver.Resolve(new InteractionRequest(player.Id, target.Id, InteractionType.Command, "stay")).Succeeded);
        world.TrySetEntityPosition(target.Id, new Vector3(12, 0, 7));
        var nav = new FakeNavigationAdapter();
        var orders = new OrderService(world, _ => nav);
        orders.Advance();
        var originalRequest = Assert.Single(nav.Requests).Key;

        Assert.Equal(ActionStartResult.Preempted, target.Actions.TryStart(
            new ActionCandidate("focused-action", ActionKind.Haul, interruptsMovementOrders: true)));
        orders.Advance();

        Assert.Equal(NavigationOutcome.Cancelled, nav.Requests[originalRequest]);
        Assert.NotNull(target.OrderIntent);
        Assert.False(orders.ShouldSuppressAutonomousBehavior(target));
        Assert.Single(nav.Requests);

        target.Actions.Complete();
        orders.Advance();

        Assert.Equal(2, nav.Requests.Count);
        var resumedRequest = Assert.Single(nav.Requests, request => request.Value == NavigationOutcome.InProgress);
        Assert.Equal(stayPosition, nav.RequestTargets[resumedRequest.Key]);
        Assert.Equal(ActionKind.CommandMove, target.Actions.Current?.Kind);
        Assert.NotNull(target.OrderIntent);
    }

    [Fact]
    public void ExhaustionAloneDoesNotSuspendAnOrderWithoutNpcRecovery()
    {
        var world = new World(needsTuning: new NeedsTuning(initialFatigue: 90));
        var issuer = world.SpawnEntity("human", Vector3.Zero);
        var follower = world.SpawnEntity("human", new Vector3(8, 0, 0));
        follower.OrderIntent = new EntityOrder(EntityOrderKind.Follow, issuer.Id);
        Assert.True(follower.Needs.IsExhausted);

        var navigator = new FakeNavigationAdapter();
        var orders = new OrderService(world, _ => navigator);
        orders.Advance();
        var route = Assert.Single(navigator.Requests);

        orders.Advance();

        Assert.Equal(NavigationOutcome.InProgress, route.Value);
        Assert.Single(navigator.Requests);
        Assert.Equal(ActionKind.CommandMove, follower.Actions.Current?.Kind);
        Assert.NotNull(follower.OrderIntent);
    }

    [Fact]
    public void FailedWildEscapeKeepsIndividualOrderPausedUntilThreatClears()
    {
        var catalog = Catalog();
        var world = new World();
        var cow = world.SpawnEntity(catalog.GetRequired("cow"), Vector3.Zero);
        var wolf = world.SpawnEntity(catalog.GetRequired("wolf"), new Vector3(-4, 0, 0));
        var issuer = world.SpawnEntity("human", new Vector3(0, 0, 2));
        cow.OrderIntent = new EntityOrder(EntityOrderKind.Stay, issuer.Id, new Vector3(0, 0, 8));

        var navigator = new FakeNavigationAdapter();
        var threat = new WildPreyThreatService(world, catalog, _ => navigator);
        var orders = new OrderService(world, _ => navigator, threat.SuppressesOtherBehavior);

        orders.Advance();
        Assert.Single(navigator.RequestTargets);
        Assert.Equal(WildPreyThreatStatus.Fleeing, threat.Advance(cow, simulationTick: 1).Status);
        var fleeRequest = navigator.RequestTargets.Last().Key;
        Assert.True(navigator.MarkUnreachable(fleeRequest));

        orders.Advance();
        Assert.Equal(WildPreyThreatStatus.Waiting, threat.Advance(cow, simulationTick: 2).Status);
        Assert.True(threat.SuppressesOtherBehavior(cow.Id));
        var orderRequestCount = navigator.RequestTargets.Count;
        var waitingReason = cow.WaitingReason;

        orders.Advance();

        Assert.Equal(orderRequestCount, navigator.RequestTargets.Count);
        Assert.Equal("Waiting", cow.Activity);
        Assert.Equal(waitingReason, cow.WaitingReason);
        Assert.Equal(new EntityOrder(EntityOrderKind.Stay, issuer.Id, new Vector3(0, 0, 8)), cow.OrderIntent);

        world.TrySetEntityPosition(wolf.Id, new Vector3(-40, 0, 0));
        Assert.Equal(WildPreyThreatStatus.NoThreat, threat.Advance(cow, simulationTick: 3).Status);
        orders.Advance();

        Assert.Equal(orderRequestCount + 1, navigator.RequestTargets.Count);
        Assert.Equal(ActionKind.CommandMove, cow.Actions.Current?.Kind);
    }

    [Fact]
    public void CommandsRequireAuthorityAndOnlyExposeFollowAndStayForMobileSpecies()
    {
        var (world, catalog, player, target, resolver) = Harness();
        Assert.False(resolver.Resolve(new InteractionRequest(player.Id, target.Id, InteractionType.Command, "follow")).Succeeded);
        Assert.Null(target.OrderIntent);

        Assert.False(resolver.Resolve(new InteractionRequest(player.Id, target.Id, InteractionType.Command, "attack")).Succeeded);
        Assert.Null(target.OrderIntent);

        foreach (var speciesId in new[] { "ape", "cow", "wolf" })
        {
            Assert.True(catalog.GetRequired(speciesId).MovementSpeed > 0);
            var creature = world.SpawnEntity(speciesId, new Vector3(12, 0, 0));
            Bond(world, player, creature);
            var result = resolver.Resolve(new InteractionRequest(player.Id, creature.Id, InteractionType.Command, "follow"));
            Assert.True(result.Succeeded, $"{speciesId}: {result.Reason}");
            Assert.Equal(new EntityOrder(EntityOrderKind.Follow, player.Id), creature.OrderIntent);
        }
    }

    [Fact]
    public void RemovingAFollowIssuerClearsTheOrderAndCancelsItsRoute()
    {
        var (world, _, player, target, resolver) = Harness();
        Bond(world, player, target);
        world.TrySetEntityPosition(target.Id, new Vector3(8, 0, 0));
        var nav = new FakeNavigationAdapter();
        var orders = new OrderService(world, _ => nav);
        Assert.True(resolver.Resolve(new InteractionRequest(player.Id, target.Id, InteractionType.Command, "follow")).Succeeded);
        orders.Advance();
        var request = Assert.Single(nav.Requests).Key;

        Assert.True(world.TryRemoveEntity(player.Id));
        orders.Advance();

        Assert.Null(target.OrderIntent);
        Assert.Null(target.Actions.Current);
        Assert.Equal(NavigationOutcome.Cancelled, nav.Requests[request]);
    }

    [Fact]
    public void SaveLoadRestoresOrderIntentAndRebuildsRoutesButLegacySavesHaveNone()
    {
        var (world, catalog, player, target, resolver) = Harness();
        Bond(world, player, target);
        world.TrySetEntityPosition(target.Id, new Vector3(9, 0, 0));
        Assert.True(resolver.Resolve(new InteractionRequest(player.Id, target.Id, InteractionType.Command, "stay")).Succeeded);
        world.TrySetEntityPosition(target.Id, new Vector3(12, 0, 0));
        var nav = new FakeNavigationAdapter();
        var orders = new OrderService(world, _ => nav);
        orders.Advance();
        var firstRequestCount = nav.Requests.Count;
        var saved = WorldSnapshotSerializer.Serialize(world, 3);

        WorldSnapshotSerializer.Load(world, saved, catalog, orders);
        var restored = Assert.Single(world.Entities, entity => entity.Id == target.Id);
        Assert.Equal(new EntityOrder(EntityOrderKind.Stay, player.Id, new Vector3(9, 0, 0)), restored.OrderIntent);
        Assert.Null(restored.Actions.Current);
        orders.Advance();
        Assert.Equal(firstRequestCount + 1, nav.Requests.Count);

        var legacy = JsonNode.Parse(saved)!.AsObject();
        legacy["SchemaVersion"] = 21;
        foreach (var entity in legacy["Entities"]!.AsArray()) entity!.AsObject().Remove("OrderIntent");
        var olderWorld = new World();
        WorldSnapshotSerializer.Load(olderWorld, legacy.ToJsonString(), catalog);
        Assert.All(olderWorld.Entities, entity => Assert.Null(entity.OrderIntent));
        Assert.Equal(WorldSnapshotSerializer.CurrentSchemaVersion, WorldSnapshotSerializer.Capture(olderWorld, 3).SchemaVersion);
    }

    [Fact]
    public void LoadingFollowWithMissingOrDeadIssuerClearsOnlyTheStaleIntent()
    {
        var (world, catalog, player, target, resolver) = Harness();
        Bond(world, player, target);
        Assert.True(resolver.Resolve(new InteractionRequest(player.Id, target.Id, InteractionType.Command, "follow")).Succeeded);
        var saved = JsonNode.Parse(WorldSnapshotSerializer.Serialize(world, 3))!.AsObject();

        var missingIssuerSave = JsonNode.Parse(saved.ToJsonString())!.AsObject();
        var missingTarget = missingIssuerSave["Entities"]!.AsArray()
            .Select(node => node!.AsObject()).Single(entity => (string?)entity["Id"] == target.Id);
        missingTarget["OrderIntent"]!["IssuerId"] = "entity-999999";

        var missingIssuerWorld = new World();
        WorldSnapshotSerializer.Load(missingIssuerWorld, missingIssuerSave.ToJsonString(), catalog);
        Assert.Null(Assert.Single(missingIssuerWorld.Entities, entity => entity.Id == target.Id).OrderIntent);

        var deadIssuerSave = JsonNode.Parse(saved.ToJsonString())!.AsObject();
        var deadIssuer = deadIssuerSave["Entities"]!.AsArray()
            .Select(node => node!.AsObject()).Single(entity => (string?)entity["Id"] == player.Id);
        deadIssuer["IsDead"] = true;
        var deadIssuerWorld = new World();
        WorldSnapshotSerializer.Load(deadIssuerWorld, deadIssuerSave.ToJsonString(), catalog);
        Assert.Null(Assert.Single(deadIssuerWorld.Entities, entity => entity.Id == target.Id).OrderIntent);
    }

    [Fact]
    public void InvalidSavedStayPositionRejectsLoadBeforeReplacingTheWorld()
    {
        var (world, catalog, player, target, resolver) = Harness();
        Bond(world, player, target);
        Assert.True(resolver.Resolve(new InteractionRequest(player.Id, target.Id, InteractionType.Command, "stay")).Succeeded);
        var before = WorldSnapshotSerializer.Serialize(world, 3);
        var invalid = JsonNode.Parse(before)!.AsObject();
        var targetNode = invalid["Entities"]!.AsArray()
            .Select(node => node!.AsObject()).Single(entity => (string?)entity["Id"] == target.Id);
        targetNode["OrderIntent"]!["Position"] = null;

        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(world, invalid.ToJsonString(), catalog));
        Assert.Equal(before, WorldSnapshotSerializer.Serialize(world, 3));
    }
}
