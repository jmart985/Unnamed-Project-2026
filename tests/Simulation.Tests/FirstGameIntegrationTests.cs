using System.Numerics;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class FirstGameIntegrationTests
{
    [Fact]
    public void FirstGameLoopIsRepeatableAndRestoresDurableStateWithoutDuplicatingResources()
    {
        Assert.Equal(RunScenario(), RunScenario());
    }

    private static string RunScenario()
    {
        var species = SpeciesCatalog.LoadFromFile(Data("species.json"));
        var events = RelationshipEventCatalog.LoadFromFile(Data("relationship_events.json"));
        var session = NewGameSession.Create(species, PlayerCharacterProfile.Create("Ada"), 42);
        var world = session.World;
        var clock = new SimulationClock();
        var navigators = world.Entities.ToDictionary(e => e.Id, _ => new FakeNavigationAdapter());
        var food = new FoodConsumptionService(world, species, e => navigators[e.Id]);
        var squad = new SquadOrderService(world, e => navigators[e.Id]);
        var hauling = new WoodHaulingService(world, e => navigators[e.Id]);
        var commands = new PlayerCommandBoundary(world, clock, new CreatureGreetingService(world, species, events));
        Assert.Equal(14, world.Entities.Count);
        Assert.Equal(400, FoodTotal(world));
        Assert.Equal(100, WoodTotal(world));
        var initialSatiety = session.Player.Satiety;
        clock.Advance(1, seconds => { foreach (var entity in world.Entities) entity.Needs.Advance(seconds, satietyOverride: species.GetRequired(entity.SpeciesId).HungerRate); });
        Assert.True(session.Player.Satiety < initialSatiety);
        var source = world.FoodSources.Single(s => s.Id == "food-pile-1");
        var move = commands.Execute(new(PlayerCommandKind.Move, session.PlayerEntityId, Target: source.Position));
        Assert.Equal(PlayerCommandResultCode.Accepted, move.Code);
        world.TrySetEntityPosition(session.PlayerEntityId, move.Target!.Value);
        var beforeEating = session.Player.Satiety;
        Assert.Equal(PlayerEatStatus.Ate, food.TryPlayerEat(source.Id).Status);
        Assert.Equal(Math.Min(100, beforeEating + session.Player.Needs.NutritionPerUnit), session.Player.Satiety);
        Assert.Equal(399, FoodTotal(world));
        Assert.Equal(1, food.FoodUnitsConsumed);

        // Explicit unavailable and out-of-range sources reject without changing the World.
        var empty = world.AddFoodSource("empty", source.Position, new ResourceContainer("empty", 1));
        var failureState = WorldSnapshotSerializer.Serialize(world, clock.SimulationTime);
        Assert.Equal(PlayerEatStatus.SourceEmpty, food.TryPlayerEat(empty.Id).Status);
        Assert.Equal(PlayerEatStatus.SourceNotFound, food.TryPlayerEat("missing").Status);
        Assert.Equal(PlayerEatStatus.OutOfRange, food.TryPlayerEat("forage-pile-cow").Status);
        Assert.Equal(failureState, WorldSnapshotSerializer.Serialize(world, clock.SimulationTime));
        Assert.Equal(1, food.FoodUnitsConsumed);

        var members = world.Entities.Where(e => e.SpeciesId == "human" && e.Id != session.PlayerEntityId).Take(2).ToArray();
        foreach (var member in members) Assert.Equal(SquadResult.Accepted, world.AddSquadMember(member.Id));
        Assert.Equal(SquadResult.Unchanged, world.AddSquadMember(members[0].Id));
        var target = new Vector3(4, 0, 3);
        Assert.Equal(SquadResult.Accepted, squad.Execute(SquadOrderKind.MoveTo, target).Code);
        Arrive(world, squad, members, navigators, target);
        Assert.Equal(SquadResult.Accepted, squad.Execute(SquadOrderKind.FollowPlayer).Code);
        Arrive(world, squad, members, navigators, session.Player.Position);
        Assert.Equal(SquadResult.Accepted, squad.Execute(SquadOrderKind.Stop).Code);
        squad.Advance();
        Assert.Equal(SquadResult.InvalidTarget, squad.Execute(SquadOrderKind.MoveTo, new(float.PositiveInfinity, 0, 0)).Code);
        Assert.Equal(SquadOrderKind.Stop, squad.Order);
        Assert.All(members, member => Assert.Null(member.Actions.Current));
        Assert.Equal(2, world.SquadMemberIds.Count);

        // Existing hauling distributes wood among ground, carried inventory, and storage.
        var hauler = world.Entities.First(e => e.SpeciesId == "human" && e.Id != session.PlayerEntityId && !world.SquadMemberIds.Contains(e.Id));
        var trip = hauling.Advance(hauler, species.GetRequired("human"), 0);
        Assert.Equal(WoodHaulingStatus.TravelingToSource, trip.Status);
        navigators[hauler.Id].CompleteAsArrived(navigators[hauler.Id].Requests.Keys.Single());
        world.TrySetEntityPosition(hauler.Id, world.ResourcePiles[0].Position);
        Assert.Equal(WoodHaulingStatus.TravelingToStockpile, hauling.Advance(hauler, species.GetRequired("human"), 1).Status);
        Assert.True(hauler.Inventory.GetValueOrDefault(ItemIds.Wood) > 0);
        Assert.Equal(100, WoodTotal(world));

        var cow = world.Entities.Single(e => e.SpeciesId == "cow");
        var wolf = world.Entities.Single(e => e.SpeciesId == "wolf");
        world.TrySetEntityPosition(session.PlayerEntityId, cow.Position + Vector3.UnitX);
        Assert.Equal(PlayerCommandResultCode.Accepted, commands.Execute(new(PlayerCommandKind.Interact, cow.Id)).Code);
        var relationship = world.Relationships.Single();
        Assert.Equal(session.PlayerEntityId, relationship.ObserverEntityId);
        Assert.Equal(cow.Id, relationship.TargetEntityId);
        Assert.Equal(5, relationship.Familiarity);
        Assert.Equal(0, relationship.Trust);
        Assert.Equal(0, relationship.Fear);
        Assert.Equal(0, relationship.Loyalty);
        failureState = WorldSnapshotSerializer.Serialize(world, clock.SimulationTime);
        Assert.Equal(PlayerCommandResultCode.InvalidEntity, commands.Execute(new(PlayerCommandKind.Interact, "missing")).Code);
        Assert.Equal(PlayerCommandResultCode.OutOfRange, commands.Execute(new(PlayerCommandKind.Interact, world.Entities.Single(e => e.SpeciesId == "ape").Id)).Code);
        Assert.Equal(PlayerCommandResultCode.UnsupportedInteraction, commands.Execute(new(PlayerCommandKind.Interact, wolf.Id)).Code);
        Assert.Equal(failureState, WorldSnapshotSerializer.Serialize(world, clock.SimulationTime));

        clock.SetPaused(true);
        var saved = WorldSnapshotSerializer.Serialize(world, clock.SimulationTime);
        var invalid = JsonNode.Parse(saved)!;
        invalid["SquadMemberIds"] = new JsonArray("missing");
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(world, invalid.ToJsonString(), species, food, squad, hauling));
        Assert.Equal(saved, WorldSnapshotSerializer.Serialize(world, clock.SimulationTime));
        Assert.Equal(ActionKind.Haul, hauler.Actions.Current?.Kind);
        var restoredTime = WorldSnapshotSerializer.Load(world, saved, species, food, squad, hauling);
        clock.Restore(restoredTime);
        Assert.True(clock.IsPaused);
        Assert.Equal(saved, WorldSnapshotSerializer.Serialize(world, clock.SimulationTime));
        Assert.Equal(399, FoodTotal(world));
        Assert.Equal(100, WoodTotal(world));
        Assert.Equal(2, world.SquadMemberIds.Count);
        Assert.Equal(SquadOrderKind.None, squad.Order);
        Assert.All(world.Entities, e => Assert.Null(e.Actions.Current));
        Assert.All(navigators.Values, nav => Assert.DoesNotContain(NavigationOutcome.InProgress, nav.Requests.Values));
        return saved;
    }

    private static void Arrive(World world, SquadOrderService squad, EntityRecord[] members, Dictionary<string, FakeNavigationAdapter> navigators, Vector3 target)
    {
        squad.Advance();
        foreach (var member in members)
        {
            var nav = navigators[member.Id];
            foreach (var request in nav.Requests.Where(r => r.Value == NavigationOutcome.InProgress)) nav.CompleteAsArrived(request.Key);
            world.TrySetEntityPosition(member.Id, target);
        }
        squad.Advance();
        Assert.All(members, e => Assert.Equal(target, e.Position));
    }

    private static long FoodTotal(World world) => world.FoodSources.Sum(s => s.Contents.Counts.Where(i => ItemIds.IsEdibleFood(i.Key)).Sum(i => (long)i.Value))
        + world.Entities.Sum(e => e.Inventory.Where(i => ItemIds.IsEdibleFood(i.Key)).Sum(i => (long)i.Value))
        + world.Stockpiles.Sum(s => s.Contents.Counts.Where(i => ItemIds.IsEdibleFood(i.Key)).Sum(i => (long)i.Value));
    private static long WoodTotal(World world) => world.ResourcePiles.Sum(s => (long)s.Contents.Counts.GetValueOrDefault(ItemIds.Wood))
        + world.Entities.Sum(e => (long)e.Inventory.GetValueOrDefault(ItemIds.Wood))
        + world.Stockpiles.Sum(s => (long)s.Contents.Counts.GetValueOrDefault(ItemIds.Wood));
    private static string Data(string name) => Path.Combine(AppContext.BaseDirectory, "data", name);
}
