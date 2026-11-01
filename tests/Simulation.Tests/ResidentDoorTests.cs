using System.Numerics;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class ResidentDoorTests
{
    private static SpeciesCatalog Catalog() => SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
    private static World Village() => NewGameSession.Create(Catalog(), PlayerCharacterProfile.Create("Doors"), createVillage: true).World;
    private static EntityRecord Resident(World world) => world.Entities.Single(entity => entity.Id == world.Structures[0].ResidentId);
    private static void Lock(World world, Structure house, EntityRecord owner)
    {
        world.TrySetEntityPosition(owner.Id, house.ToWorld(new(0, 0, HouseGeometry.DoorZ - 1.25f)));
        Assert.True(world.OperateDoor(owner.Id, house.Id, HouseDoorAction.Lock).Succeeded);
        world.TrySetEntityPosition(owner.Id, house.Position);
    }

    [Theory]
    [InlineData(0)] [InlineData(60)]
    public void HungryResidentUnlocksExitsAndEatsDuringWorkOrRest(double time)
    {
        var world = Village();
        var owner = Resident(world);
        var house = world.Structures[0];
        Lock(world, house, owner);
        owner.Needs.Advance(1, satietyOverride: owner.Satiety - 20, hydrationOverride: 0, fatigueOverride: 0);
        var contents = new ResourceContainer("outside-food", 10);
        contents.TryAdd(ItemIds.Food, 2);
        var source = world.AddFoodSource("outside-food", house.ToWorld(new(0, 0, 8)), contents);
        var nav = new FakeNavigationAdapter();
        var food = new FoodConsumptionService(world, Catalog(), _ => nav);
        var work = new WoodHaulingService(world, _ => nav);
        var employment = world.GetEmployment(owner.Id);
        work.ReconcileSchedule(owner, time);
        Assert.Equal(source, food.Advance(owner, Catalog().GetRequired("human"), 0).Source);
        var blockedFoodRequest = nav.Requests.Last().Key;
        work.ReconcileSchedule(owner, time);
        Assert.Equal(FoodConsumptionStatus.Traveling, food.Advance(owner, Catalog().GetRequired("human"), 1).Status);
        Assert.Equal(NavigationOutcome.Cancelled, nav.Requests[blockedFoodRequest]);
        Assert.True(house.IsLocked);
        Assert.Equal(ActionKind.Eat, owner.Actions.Current!.Kind);
        var doorstep = nav.RequestTargets.Last();
        Assert.Equal(house.ToWorld(new(0, 0, HouseGeometry.DoorZ - 1.25f)), doorstep.Value);
        world.TrySetEntityPosition(owner.Id, doorstep.Value);
        work.ReconcileSchedule(owner, time);
        Assert.Equal(source, food.Advance(owner, Catalog().GetRequired("human"), 2).Source);
        Assert.False(house.IsClosed);
        Assert.False(house.IsLocked);
        Assert.Equal(NavigationOutcome.Cancelled, nav.Requests[doorstep.Key]);
        Assert.Equal(source.Position, nav.RequestTargets.Last().Value);
        nav.CompleteAsArrived(nav.Requests.Last().Key);
        world.TrySetEntityPosition(owner.Id, source.Position);
        Assert.Equal(FoodConsumptionStatus.Ate, food.Advance(owner, Catalog().GetRequired("human"), 3).Status);
        Assert.False(owner.Needs.IsHungry);
        Assert.Equal(1, source.Contents.Counts[ItemIds.Food]);
        Assert.Equal(employment, world.GetEmployment(owner.Id));
    }

    [Fact]
    public void FoodInsideHomeDoesNotRequireUnlockingOrLeaving()
    {
        var world = Village();
        var owner = Resident(world);
        var house = world.Structures[0];
        Lock(world, house, owner);
        owner.Needs.Advance(1, satietyOverride: owner.Satiety - 20, hydrationOverride: 0, fatigueOverride: 0);
        var contents = new ResourceContainer("inside-food", 2);
        contents.TryAdd(ItemIds.Food, 2);
        var source = world.AddFoodSource("inside-food", house.Position + Vector3.UnitX, contents);
        var nav = new FakeNavigationAdapter();
        var food = new FoodConsumptionService(world, Catalog(), _ => nav);
        food.Advance(owner, Catalog().GetRequired("human"), 0);
        nav.CompleteAsArrived(nav.Requests.Single().Key);
        world.TrySetEntityPosition(owner.Id, source.Position);
        Assert.Equal(FoodConsumptionStatus.Ate, food.Advance(owner, Catalog().GetRequired("human"), 1).Status);
        Assert.True(house.IsLocked);
        Assert.Single(nav.Requests);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FreeStorageCancelsHomeJourneyAndImmediatelyResumesDelivery(bool arrivedAtHome)
    {
        var world = Village();
        var owner = Resident(world);
        var house = world.Structures[0];
        var stockpile = world.Stockpiles.Single();
        stockpile.Contents.TryAdd(ItemIds.Wood, stockpile.Contents.Capacity);
        owner.TryAddCarried(ItemIds.Wood, 5);
        world.TrySetEntityPosition(owner.Id, house.ToWorld(new(0, 0, 8)));
        var nav = new FakeNavigationAdapter();
        var work = new WoodHaulingService(world, _ => nav);
        Assert.True(work.ReconcileSchedule(owner, 60));
        var homeRequest = nav.Requests.Single().Key;
        if (arrivedAtHome)
        {
            world.TrySetEntityPosition(owner.Id, house.Position);
            nav.CompleteAsArrived(homeRequest);
        }
        stockpile.Contents.TryRemove(ItemIds.Wood, 5);
        Assert.False(work.ReconcileSchedule(owner, 60));
        Assert.Null(owner.Actions.Current);
        Assert.NotEqual(NavigationOutcome.InProgress, nav.Requests[homeRequest]);
        var delivery = work.Advance(owner, Catalog().GetRequired("human"), 2, 60);
        Assert.Equal(WoodHaulingStatus.TravelingToStockpile, delivery.Status);
        Assert.Equal(stockpile.Position, nav.RequestTargets.Last().Value);
        nav.CompleteAsArrived(nav.Requests.Last().Key);
        world.TrySetEntityPosition(owner.Id, stockpile.Position);
        Assert.Equal(WoodHaulingStatus.Delivered, work.Advance(owner, Catalog().GetRequired("human"), 3, 60).Status);
        Assert.Equal(100, stockpile.Contents.Counts[ItemIds.Wood]);
        Assert.Empty(owner.Inventory);
    }

    [Fact]
    public void OwnerCanOperateLockAndUnlockButOutsiderOnlyHasInteriorEgress()
    {
        var world = Village();
        var owner = Resident(world);
        var house = world.Structures[0];
        Lock(world, house, owner);
        world.TrySetEntityPosition(world.PlayerEntityId!, house.ToWorld(new(0, 0, 5.1f)));
        foreach (var action in new[] { HouseDoorAction.Toggle, HouseDoorAction.Open, HouseDoorAction.Unlock, HouseDoorAction.Lock })
            Assert.False(world.OperateDoor(world.PlayerEntityId!, house.Id, action).Succeeded);
        Assert.True(house.IsLocked && house.IsClosed);
        world.TrySetEntityPosition(owner.Id, house.ToWorld(new(0, 0, 2.6f)));
        Assert.True(world.OperateDoor(owner.Id, house.Id, HouseDoorAction.Unlock).Succeeded);
        Assert.True(house.IsClosed && !house.IsLocked);
        Assert.True(world.OperateDoor(owner.Id, house.Id, HouseDoorAction.Open).Succeeded);
        Assert.False(house.IsClosed);
        Assert.True(world.OperateDoor(owner.Id, house.Id, HouseDoorAction.Close).Succeeded);
        Assert.True(world.OperateDoor(owner.Id, house.Id, HouseDoorAction.Lock).Succeeded);
        world.TrySetEntityPosition(world.PlayerEntityId!, house.ToWorld(new(0, 0, 2.6f)));
        Assert.True(world.OperateDoor(world.PlayerEntityId!, house.Id, HouseDoorAction.Open).Succeeded);
        Assert.False(house.IsLocked || house.IsClosed);
    }

    [Fact]
    public void ResidentSecuresDoorDuringRestAndOpensItForNextShift()
    {
        var world = Village();
        var owner = Resident(world);
        var house = world.Structures[0];
        world.TrySetEntityPosition(owner.Id, house.ToWorld(new(0, 0, 2.6f)));
        var nav = new FakeNavigationAdapter();
        var work = new WoodHaulingService(world, _ => nav);
        Assert.True(work.ReconcileSchedule(owner, 60));
        Assert.True(house.IsClosed && house.IsLocked);
        Assert.Equal(house.Position, nav.RequestTargets.Single().Value);
        world.TrySetEntityPosition(owner.Id, house.Position);
        work.ReconcileSchedule(owner, 60);
        Assert.Equal("Resting at home", owner.Activity);
        Assert.True(work.ReconcileSchedule(owner, 120));
        var exit = nav.RequestTargets.Last().Value;
        world.TrySetEntityPosition(owner.Id, exit);
        Assert.False(work.ReconcileSchedule(owner, 120));
        Assert.False(house.IsClosed || house.IsLocked);
    }

    [Fact]
    public void LockPersistsButVanishedResidentLeavesHouseUnlocked()
    {
        var world = Village();
        var owner = Resident(world);
        var house = world.Structures[0];
        Lock(world, house, owner);
        var loaded = new World();
        WorldSnapshotSerializer.Load(loaded, WorldSnapshotSerializer.Serialize(world, 60), Catalog());
        var restored = loaded.Structures.Single(item => item.Id == house.Id);
        Assert.True(restored.IsLocked && restored.IsClosed);
        loaded.TryRemoveEntity(owner.Id);
        Assert.Null(restored.ResidentId);
        Assert.False(restored.IsLocked);
    }

    [Theory]
    [InlineData("open")] [InlineData("unowned")]
    public void InvalidLockedStateRejectsBeforeReplacingWorld(string defect)
    {
        var world = Village();
        Lock(world, world.Structures[0], Resident(world));
        var before = WorldSnapshotSerializer.Serialize(world, 60);
        var json = JsonNode.Parse(before)!;
        if (defect == "open") json["Structures"]![0]!["IsClosed"] = false;
        else json["Structures"]![0]!["ResidentId"] = null;
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(world, json.ToJsonString(), Catalog()));
        Assert.Equal(before, WorldSnapshotSerializer.Serialize(world, 60));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExplicitMovementRoutesViaOwnersHandleWithoutReplacingOrderIntent(bool squadOrder)
    {
        var world = Village();
        var owner = Resident(world);
        var house = world.Structures[0];
        Lock(world, house, owner);
        world.TrySetEntityPosition(world.PlayerEntityId!, house.ToWorld(new(0, 0, 9)));
        var nav = new FakeNavigationAdapter();
        if (squadOrder)
        {
            world.AddSquadMember(owner.Id);
            var squad = new SquadOrderService(world, _ => nav);
            Assert.Equal(SquadResult.Accepted, squad.Execute(SquadOrderKind.MoveTo, house.ToWorld(new(0, 0, 9))).Code);
            squad.Advance();
            Assert.Equal(house.ToWorld(new(0, 0, 2.6f)), nav.RequestTargets.Last().Value);
            var initialRequest = nav.Requests.Last().Key;
            world.TrySetEntityPosition(owner.Id, nav.RequestTargets.Last().Value);
            squad.Advance();
            Assert.False(house.IsClosed || house.IsLocked);
            Assert.Equal(ActionKind.SquadMove, owner.Actions.Current!.Kind);
            Assert.Equal(NavigationOutcome.Cancelled, nav.Requests[initialRequest]);
            Assert.Equal(SquadOrderKind.MoveTo, squad.Order);
            Assert.Equal(house.ToWorld(new(0, 0, 9)), nav.RequestTargets.Last().Value);
        }
        else
        {
            owner.OrderIntent = new EntityOrder(EntityOrderKind.Follow, world.PlayerEntityId!);
            var intent = owner.OrderIntent;
            var orders = new OrderService(world, _ => nav);
            orders.Advance();
            Assert.Equal(house.ToWorld(new(0, 0, 2.6f)), nav.RequestTargets.Last().Value);
            var action = owner.Actions.Current!.Id;
            world.TrySetEntityPosition(owner.Id, nav.RequestTargets.Last().Value);
            orders.Advance();
            Assert.False(house.IsClosed || house.IsLocked);
            Assert.Equal(intent, owner.OrderIntent);
            Assert.Equal(action, owner.Actions.Current!.Id);
            Assert.True(!HouseGeometry.Contains(house, nav.RequestTargets.Last().Value));
        }
    }

    [Theory]
    [InlineData("reset")] [InlineData("load")] [InlineData("flee")] [InlineData("invalid-load")]
    public void DoorExitCleanupRespectsInterruptionAndLoadAtomicity(string trigger)
    {
        var world = Village();
        var owner = Resident(world);
        Lock(world, world.Structures[0], owner);
        owner.Needs.Advance(1, satietyOverride: owner.Satiety - 20, hydrationOverride: 0, fatigueOverride: 0);
        var nav = new FakeNavigationAdapter();
        var food = new FoodConsumptionService(world, Catalog(), _ => nav);
        food.Advance(owner, Catalog().GetRequired("human"), 0);
        food.Advance(owner, Catalog().GetRequired("human"), 1);
        var request = nav.Requests.Last().Key;
        var actionId = owner.Actions.Current!.Id;
        Assert.StartsWith("food-door-", actionId);
        if (trigger == "reset") food.Reset();
        if (trigger == "load")
        {
            WorldSnapshotSerializer.Load(world, WorldSnapshotSerializer.Serialize(world, 60), Catalog(), food);
            Assert.True(world.Structures[0].IsLocked);
            Assert.Null(world.Entities.Single(entity => entity.Id == owner.Id).Actions.Current);
        }
        if (trigger == "flee")
        {
            Assert.Equal(ActionStartResult.Preempted, owner.Actions.TryStart(new ActionCandidate("escape", ActionKind.Flee)));
            food.Advance(owner, Catalog().GetRequired("human"), 2);
            Assert.Equal("escape", owner.Actions.Current!.Id);
            Assert.True(world.Structures[0].IsLocked);
        }
        if (trigger == "invalid-load")
        {
            var before = WorldSnapshotSerializer.Serialize(world, 60);
            var json = JsonNode.Parse(before)!;
            json["Structures"]![0]!["IsClosed"] = false;
            Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(world, json.ToJsonString(), Catalog(), food));
            Assert.Equal(before, WorldSnapshotSerializer.Serialize(world, 60));
            Assert.Equal(actionId, owner.Actions.Current!.Id);
            Assert.Equal(NavigationOutcome.InProgress, nav.Requests[request]);
        }
        else Assert.Equal(NavigationOutcome.Cancelled, nav.Requests[request]);
    }
}
