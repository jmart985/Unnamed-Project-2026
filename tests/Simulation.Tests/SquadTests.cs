using System.Numerics;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class SquadTests
{
    private static SpeciesCatalog Catalog() => SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
    private static World Session() => NewGameSession.Create(Catalog(), PlayerCharacterProfile.Create("Ada"), 42).World;
    private static EntityRecord[] Members(World world) => world.Entities.Where(e => e.SpeciesId == "human" && e.Id != world.PlayerEntityId).Take(2).ToArray();

    [Fact]
    public void FollowStopsOutsidePlayerAndResumesOnlyBeyondOuterBand()
    {
        var world = Session();
        var member = Members(world)[0];
        world.AddSquadMember(member.Id);
        world.TrySetEntityPosition(world.PlayerEntityId, Vector3.Zero);
        world.TrySetEntityPosition(member.Id, new(8, 0, 0));
        var nav = new FakeNavigationAdapter();
        var squad = new SquadOrderService(world, _ => nav);
        squad.Execute(SquadOrderKind.FollowPlayer);
        squad.Advance();
        Assert.Equal(ActionKind.SquadMove, member.Actions.Current?.Kind);
        var stopped = squad.ConstrainFollowStep(member.Id, member.Position, Vector3.Zero);
        Assert.InRange(stopped.X, 2, 2.001f);
        world.TrySetEntityPosition(member.Id, stopped);
        squad.Advance();
        Assert.Null(member.Actions.Current);
        Assert.Contains("holding distance", member.Activity);
        for (var i = 0; i < 100; i++) squad.Advance();
        Assert.Single(nav.Requests);
        world.TrySetEntityPosition(world.PlayerEntityId, new(stopped.X - 3, 0, 0));
        squad.Advance();
        Assert.Single(nav.Requests); // Exactly 3 m does not resume.
        world.TrySetEntityPosition(world.PlayerEntityId, new(stopped.X - 3.01f, 0, 0));
        squad.Advance();
        Assert.Equal(2, nav.Requests.Count);
        Assert.Equal(ActionKind.SquadMove, member.Actions.Current?.Kind);
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(1, 0)] [InlineData(0, 1)]
    public void FollowStepClipsCrossingSegmentsAgainstLivePlayer(float playerX, float playerZ)
    {
        var world = Session();
        var member = Members(world)[0];
        world.AddSquadMember(member.Id);
        world.TrySetEntityPosition(world.PlayerEntityId, Vector3.Zero);
        world.TrySetEntityPosition(member.Id, new(10, 0, 0));
        var squad = new SquadOrderService(world, _ => new FakeNavigationAdapter());
        squad.Execute(SquadOrderKind.FollowPlayer);
        squad.Advance();
        var player = new Vector3(playerX, 0, playerZ);
        world.TrySetEntityPosition(world.PlayerEntityId, player); // Existing path still targets old position.
        var current = player + new Vector3(5, 0, 0);
        var clipped = squad.ConstrainFollowStep(member.Id, current, player - new Vector3(5, 0, 0));
        Assert.InRange(Vector3.Distance(clipped, player), 2, 2.001f);
        Assert.True(clipped.X > player.X); // Endpoint alone would permit tunneling through the player.
        var away = current + Vector3.UnitX;
        Assert.Equal(away, squad.ConstrainFollowStep(member.Id, current, away));
        var inside = player + Vector3.UnitX;
        Assert.Equal(inside, squad.ConstrainFollowStep(member.Id, inside, player));
        Assert.Equal(current, squad.ConstrainFollowStep(member.Id, current, new(float.NaN, 0, 0)));
        squad.Execute(SquadOrderKind.MoveTo, player);
        Assert.Equal(player, squad.ConstrainFollowStep(member.Id, current, player));
    }

    [Fact]
    public void FailedFollowWaitsWithoutChurnThenRecoversAfterPlayerMoves()
    {
        var world = Session();
        var member = Members(world)[0];
        world.AddSquadMember(member.Id);
        world.TrySetEntityPosition(world.PlayerEntityId, Vector3.Zero);
        world.TrySetEntityPosition(member.Id, new(8, 0, 0));
        var nav = new FakeNavigationAdapter();
        var squad = new SquadOrderService(world, _ => nav);
        squad.Execute(SquadOrderKind.FollowPlayer);
        squad.Advance();
        nav.MarkUnreachable(nav.Requests.Keys.Single());
        squad.Advance();
        for (var i = 0; i < 100; i++) squad.Advance();
        Assert.Single(nav.Requests);
        Assert.Contains("Unreachable", member.WaitingReason);
        world.TrySetEntityPosition(world.PlayerEntityId, new(-2, 0, 0));
        squad.Advance();
        Assert.Equal(2, nav.Requests.Count);
        world.TryRemoveEntity(world.PlayerEntityId);
        var current = member.Position;
        Assert.Equal(current, squad.ConstrainFollowStep(member.Id, current, Vector3.Zero));
        squad.Advance();
        Assert.Equal(SquadOrderKind.None, squad.Order);
        Assert.Null(member.Actions.Current);
        Assert.DoesNotContain(NavigationOutcome.InProgress, nav.Requests.Values);
    }

    [Fact]
    public void MembershipRejectsIneligibleAndRemovedEntitiesWithoutMutation()
    {
        var world = Session();
        var members = Members(world);
        Assert.Equal(SquadResult.IneligibleEntity, world.AddSquadMember(world.PlayerEntityId));
        Assert.Equal(SquadResult.IneligibleEntity, world.AddSquadMember(world.Entities.First(e => e.SpeciesId == "wolf").Id));
        Assert.Equal(SquadResult.InvalidEntity, world.AddSquadMember("missing"));
        foreach (var member in members) Assert.Equal(SquadResult.Accepted, world.AddSquadMember(member.Id));
        Assert.Equal(SquadResult.Unchanged, world.AddSquadMember(members[0].Id));
        Assert.Equal(2, world.SquadMemberIds.Count);
        Assert.Equal(SquadResult.Accepted, world.RemoveSquadMember(members[0].Id));
        Assert.Equal(SquadResult.NotMember, world.RemoveSquadMember(members[0].Id));
        Assert.True(world.TryRemoveEntity(members[1].Id));
        Assert.Equal(SquadResult.InvalidEntity, world.AddSquadMember(members[1].Id));
        Assert.Equal(SquadResult.InvalidEntity, world.RemoveSquadMember(members[1].Id));
        Assert.Empty(world.SquadMemberIds);
    }

    [Fact]
    public void MoveFollowStopReuseNavigationAndRejectOrdersAtomically()
    {
        var world = Session();
        var members = Members(world);
        var navigators = members.ToDictionary(e => e.Id, _ => new FakeNavigationAdapter());
        var squad = new SquadOrderService(world, e => navigators[e.Id]);
        Assert.Equal(SquadResult.EmptySquad, squad.Execute(SquadOrderKind.Stop).Code);
        foreach (var member in members) world.AddSquadMember(member.Id);
        var target = new Vector3(10, 0, 4);
        Assert.Equal(SquadResult.Accepted, squad.Execute(SquadOrderKind.MoveTo, target).Code);
        squad.Advance();
        foreach (var nav in navigators.Values) Assert.Single(nav.Requests);
        Assert.Equal(SquadResult.Unchanged, squad.Execute(SquadOrderKind.MoveTo, target).Code);
        squad.Advance();
        Assert.Equal(SquadResult.InvalidTarget, squad.Execute(SquadOrderKind.MoveTo, new(float.NaN, 0, 0)).Code);
        Assert.Equal(SquadResult.InvalidOrder, squad.Execute((SquadOrderKind)99).Code);
        Assert.Equal(target, squad.Target);
        foreach (var nav in navigators.Values) Assert.Equal(NavigationOutcome.InProgress, Assert.Single(nav.Requests).Value);
        foreach (var member in members)
        {
            var requestId = navigators[member.Id].Requests.Keys.Single();
            world.TrySetEntityPosition(member.Id, navigators[member.Id].RequestTargets[requestId]);
            navigators[member.Id].CompleteAsArrived(requestId);
        }
        squad.Advance();
        foreach (var member in members) { Assert.Null(member.Actions.Current); Assert.Equal("Squad arrived", member.Activity); }
        Assert.Equal(SquadResult.Accepted, squad.Execute(SquadOrderKind.FollowPlayer).Code);
        squad.Advance();
        foreach (var nav in navigators.Values) Assert.Equal(2, nav.Requests.Count);
        Assert.Equal(SquadResult.Unchanged, squad.Execute(SquadOrderKind.FollowPlayer).Code);
        world.TrySetEntityPosition(world.PlayerEntityId, new Vector3(20, 0, 10));
        squad.Advance();
        foreach (var nav in navigators.Values) Assert.Equal(3, nav.Requests.Count);
        Assert.Equal(SquadResult.Accepted, squad.Execute(SquadOrderKind.Stop).Code);
        squad.Advance();
        foreach (var member in members) { Assert.Null(member.Actions.Current); Assert.Equal("Squad stopped", member.Activity); }
        foreach (var nav in navigators.Values) Assert.DoesNotContain(NavigationOutcome.InProgress, nav.Requests.Values);
        world.TryRemoveEntity(world.PlayerEntityId);
        Assert.Equal(SquadResult.PlayerUnavailable, squad.Execute(SquadOrderKind.FollowPlayer).Code);
        Assert.Equal(SquadOrderKind.Stop, squad.Order);
    }

    [Fact]
    public void SuspendedMemberKeepsTemporaryRouteAndResumesCurrentSquadOrder()
    {
        var world = Session();
        var member = Members(world)[0];
        world.AddSquadMember(member.Id);
        var navigator = new FakeNavigationAdapter();
        var squad = new SquadOrderService(world, _ => navigator);
        var squadTarget = new Vector3(20, 0, 5);
        Assert.Equal(SquadResult.Accepted, squad.Execute(SquadOrderKind.MoveTo, squadTarget).Code);
        squad.Advance();
        var originalRequestId = navigator.Requests.Keys.Single();

        squad.SuspendMember(member.Id);
        Assert.Equal(NavigationOutcome.Cancelled, navigator.Requests[originalRequestId]);
        var testTarget = member.Position + new Vector3(4, 0, 0);
        var testRequestId = navigator.Request(new NavigationRequest(testTarget));
        squad.Advance();
        Assert.Equal(NavigationOutcome.InProgress, navigator.Requests[testRequestId]);
        Assert.Equal(2, navigator.Requests.Count);

        squad.ResumeMember(member.Id);
        squad.Advance();

        Assert.Equal(3, navigator.Requests.Count);
        Assert.Equal(ActionKind.SquadMove, member.Actions.Current?.Kind);
        var resumedRequestId = navigator.Requests.Keys.Order(StringComparer.Ordinal).Last();
        Assert.Equal(squadTarget, navigator.RequestTargets[resumedRequestId]);
    }

    [Fact]
    public void MoveToAssignsStableNearbySlotsWithoutChangingTheRequestedTarget()
    {
        var world = Session();
        var members = Members(world);
        foreach (var member in members) world.AddSquadMember(member.Id);
        var navigators = members.ToDictionary(member => member.Id, _ => new FakeNavigationAdapter());
        var squad = new SquadOrderService(world, entity => navigators[entity.Id]);
        var requestedTarget = new Vector3(4, 0, 3);

        Assert.Equal(SquadResult.Accepted, squad.Execute(SquadOrderKind.MoveTo, requestedTarget).Code);
        squad.Advance();

        var assignedTargets = members.ToDictionary(member => member.Id, member =>
        {
            var requestId = navigators[member.Id].Requests.Keys.Single();
            return navigators[member.Id].RequestTargets[requestId];
        });

        Assert.Equal(requestedTarget, squad.Target);
        Assert.Equal(1.25f, Vector3.Distance(assignedTargets[members[0].Id], assignedTargets[members[1].Id]), precision: 5);
        Assert.All(assignedTargets.Values, assigned =>
            Assert.InRange(Vector3.Distance(assigned, requestedTarget), 0.624f, 0.626f));

        foreach (var member in members)
        {
            var navigator = navigators[member.Id];
            var requestId = navigator.Requests.Keys.Single();
            world.TrySetEntityPosition(member.Id, assignedTargets[member.Id]);
            navigator.CompleteAsArrived(requestId);
        }

        squad.Advance();
        Assert.All(members, member => Assert.Equal("Squad arrived", member.Activity));
    }

    [Fact]
    public void FollowAssignsSeparatedTargetsAroundPlayer()
    {
        var world = Session();
        var members = Members(world);
        world.TrySetEntityPosition(world.PlayerEntityId, Vector3.Zero);
        world.TrySetEntityPosition(members[0].Id, new(8, 0, 0));
        world.TrySetEntityPosition(members[1].Id, new(9, 0, 0));
        foreach (var member in members) world.AddSquadMember(member.Id);
        var navigators = members.ToDictionary(member => member.Id, _ => new FakeNavigationAdapter());
        var squad = new SquadOrderService(world, entity => navigators[entity.Id]);

        Assert.Equal(SquadResult.Accepted, squad.Execute(SquadOrderKind.FollowPlayer).Code);
        squad.Advance();

        var targets = members.Select(member =>
        {
            var navigator = navigators[member.Id];
            return navigator.RequestTargets[navigator.Requests.Keys.Single()];
        }).ToArray();

        Assert.InRange(Vector2.Distance(new(targets[0].X, targets[0].Z), new(targets[1].X, targets[1].Z)), 1.249f, 1.251f);
        Assert.All(targets, target => Assert.InRange(new Vector2(target.X, target.Z).Length(), 2.099f, 2.101f));
    }

    [Fact]
    public void FollowRebuildsFormationWhenMembersJoinAnActiveOrder()
    {
        var world = Session();
        var members = world.Entities
            .Where(entity => entity.SpeciesId == "human" && entity.Id != world.PlayerEntityId)
            .OrderBy(entity => entity.Id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(NewGameSession.HumanNpcCount, members.Length);
        world.TrySetEntityPosition(world.PlayerEntityId, Vector3.Zero);
        world.TrySetEntityPosition(members[0].Id, new(8, 0, 0));
        world.AddSquadMember(members[0].Id);

        var navigators = members.ToDictionary(member => member.Id, _ => new FakeNavigationAdapter());
        var squad = new SquadOrderService(world, entity => navigators[entity.Id]);
        Assert.Equal(SquadResult.Accepted, squad.Execute(SquadOrderKind.FollowPlayer).Code);
        squad.Advance();
        var originalRequest = navigators[members[0].Id].Requests.Single().Key;

        for (var index = 1; index < members.Length; index++)
        {
            var angle = MathF.Tau * index / (members.Length - 1);
            world.TrySetEntityPosition(members[index].Id, new(6 * MathF.Cos(angle), 0, 6 * MathF.Sin(angle)));
            members[index].Actions.TryStart(new ActionCandidate($"wander-{members[index].Id}", ActionKind.Wander));
            world.AddSquadMember(members[index].Id);
        }

        squad.Advance();

        Assert.Equal(NavigationOutcome.Cancelled, navigators[members[0].Id].Requests[originalRequest]);
        Assert.All(members, member => Assert.Equal(ActionKind.SquadMove, member.Actions.Current?.Kind));
        var targets = members.Select(member =>
        {
            var navigator = navigators[member.Id];
            var activeRequest = Assert.Single(navigator.Requests, pair => pair.Value == NavigationOutcome.InProgress);
            return navigator.RequestTargets[activeRequest.Key];
        }).ToArray();

        Assert.All(targets, target => Assert.InRange(new Vector2(target.X, target.Z).Length(), 2.099f, 2.101f));
        for (var first = 0; first < targets.Length; first++)
        for (var second = first + 1; second < targets.Length; second++)
            Assert.True(Vector2.Distance(new(targets[first].X, targets[first].Z), new(targets[second].X, targets[second].Z)) >= 1.29f);
    }

    [Fact]
    public void HungerPreemptsMovementAndRemovingMemberCancelsItsRequest()
    {
        var world = Session();
        var member = Members(world)[0];
        world.AddSquadMember(member.Id);
        var nav = new FakeNavigationAdapter();
        var squad = new SquadOrderService(world, _ => nav);
        squad.Execute(SquadOrderKind.MoveTo, new(10, 0, 10));
        squad.Advance();
        Assert.Equal(ActionStartResult.Preempted, member.Actions.TryStart(new("eat", ActionKind.Eat)));
        Assert.Equal(NavigationOutcome.Cancelled, nav.Requests.Values.Single());
        squad.Advance();
        Assert.Single(nav.Requests);
        member.Actions.Complete();
        squad.Advance();
        Assert.Equal(2, nav.Requests.Count);
        world.RemoveSquadMember(member.Id);
        Assert.Null(member.Actions.Current);
        Assert.DoesNotContain(NavigationOutcome.InProgress, nav.Requests.Values);
    }

    [Fact]
    public void ArrivedMemberReturnsAfterFoodPreemptionMovesItAway()
    {
        var world = Session();
        var member = Members(world)[0];
        world.AddSquadMember(member.Id);
        var nav = new FakeNavigationAdapter();
        var squad = new SquadOrderService(world, _ => nav);
        var goal = new Vector3(10, 0, 10);
        squad.Execute(SquadOrderKind.MoveTo, goal);
        squad.Advance();
        world.TrySetEntityPosition(member.Id, goal);
        squad.Advance();
        Assert.Equal("Squad arrived", member.Activity);
        member.Actions.TryStart(new("eat", ActionKind.Eat));
        world.TrySetEntityPosition(member.Id, Vector3.Zero);
        squad.Advance();
        Assert.Single(nav.Requests);
        member.Actions.Complete();
        squad.Advance();
        Assert.Equal(2, nav.Requests.Count);
        Assert.Equal(ActionKind.SquadMove, member.Actions.Current?.Kind);
    }

    [Fact]
    public void SnapshotRestoresMembersAndClearsOrdersOnlyAfterValidation()
    {
        var world = Session();
        var members = Members(world);
        foreach (var member in members) world.AddSquadMember(member.Id);
        var nav = new FakeNavigationAdapter();
        var squad = new SquadOrderService(world, _ => nav);
        squad.Execute(SquadOrderKind.MoveTo, new(10, 0, 10));
        squad.Advance();
        var json = WorldSnapshotSerializer.Serialize(world, 2);
        foreach (var ids in new[] { new[] { "missing" }, new[] { members[0].Id, members[0].Id }, new[] { world.PlayerEntityId! }, new[] { world.Entities.First(e => e.SpeciesId == "wolf").Id } })
        {
            var invalid = JsonNode.Parse(json)!;
            invalid["SquadMemberIds"] = new JsonArray(ids.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray());
            Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(world, invalid.ToJsonString(), Catalog(), squad));
            Assert.Equal(json, WorldSnapshotSerializer.Serialize(world, 2));
            Assert.Equal(SquadOrderKind.MoveTo, squad.Order);
        }
        Assert.Equal(2, WorldSnapshotSerializer.Load(world, json, Catalog(), squad));
        Assert.Equal(members.Select(e => e.Id).Order(), world.SquadMemberIds);
        Assert.Equal(SquadOrderKind.None, squad.Order);
        Assert.All(world.Entities, e => Assert.Null(e.Actions.Current));
        Assert.DoesNotContain(NavigationOutcome.InProgress, nav.Requests.Values);
    }

    [Theory]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void LegacySchemasMigrateWithoutMembersAndPreserveVersionEightOwnership(int version)
    {
        var world = Session();
        world.DefineFaction("settlers");
        world.SetStockpileFactionOwner(world.Stockpiles[0].Id, "settlers");
        var json = JsonNode.Parse(WorldSnapshotSerializer.Serialize(world, 0))!;
        json["SchemaVersion"] = version;
        json.AsObject().Remove("SquadMemberIds");
        WorldSnapshotSerializer.Load(world, json.ToJsonString(), Catalog());
        Assert.Empty(world.SquadMemberIds);
        Assert.Equal(version == 8 ? "settlers" : null, world.Stockpiles[0].FactionOwnerId);
        Assert.Equal(WorldSnapshotSerializer.CurrentSchemaVersion, WorldSnapshotSerializer.Capture(world, 0).SchemaVersion);
    }

    [Fact]
    public void FailedNavigationWaitsWithoutRequestChurnAndLoadRequiresMembership()
    {
        var world = Session();
        var member = Members(world)[0];
        world.AddSquadMember(member.Id);
        var nav = new FakeNavigationAdapter();
        var squad = new SquadOrderService(world, _ => nav);
        squad.Execute(SquadOrderKind.MoveTo, new(10, 0, 10));
        squad.Advance();
        nav.MarkUnreachable(nav.Requests.Keys.Single());
        squad.Advance();
        Assert.Equal("Squad waiting", member.Activity);
        Assert.Contains("Unreachable", member.WaitingReason);
        squad.Advance();
        Assert.Single(nav.Requests);
        Assert.Null(member.Actions.Current);
        var invalid = JsonNode.Parse(WorldSnapshotSerializer.Serialize(world, 0))!;
        invalid.AsObject().Remove("SquadMemberIds");
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(world, invalid.ToJsonString(), Catalog(), squad));
        Assert.Equal(SquadOrderKind.MoveTo, squad.Order);
        squad.Execute(SquadOrderKind.Stop);
        squad.Execute(SquadOrderKind.MoveTo, new(10, 0, 10));
        squad.Advance();
        Assert.Equal(2, nav.Requests.Count);
    }
}
