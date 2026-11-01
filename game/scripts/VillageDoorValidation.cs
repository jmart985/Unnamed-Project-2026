using Godot;
using Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace UnnamedProject2026;

public partial class Main
{
    private async Task ValidateVillageDoorRecovery()
    {
        StartSession(PlayerCharacterProfile.Create("Resident door recovery"), 42, "Door recovery.", createVillage: true);
        SetProcess(false);
        _playerCharacter.SetPhysicsProcess(false);
        _playerCharacter.GlobalPosition = new(0, 0, -10);
        SyncPlayerPosition();
        var house = _world.Structures[0];
        var worker = _world.Entities.Single(entity => entity.Id == house.ResidentId);
        var species = _speciesCatalog.GetRequired(worker.SpeciesId);
        var employmentId = _world.GetEmployment(worker.Id)!.Id;
        var sourceContents = new ResourceContainer("door-food", 2);
        sourceContents.TryAdd(ItemIds.Food, 2);
        var source = _world.AddFoodSource("door-food", house.ToWorld(new(0, 0, 8)), sourceContents);
        foreach (var phase in new[] { 0d, 60d })
        {
            _foodConsumption!.Reset();
            _woodHauling!.Reset();
            _clock.Restore(phase);
            _clock.SetPaused(false);
            _world.TrySetEntityPosition(worker.Id, house.ToWorld(new(0, 0, 2.6f)));
            Require(_world.OperateDoor(worker.Id, house.Id, HouseDoorAction.Lock).Succeeded, "resident locks its own door");
            _world.TrySetEntityPosition(worker.Id, house.Position);
            worker.Needs.Advance(1, satietyOverride: worker.Satiety - 20, hydrationOverride: 0, fatigueOverride: 0);
            SyncHouseDoorNavigation();
            SyncEntityViews();
            for (var frame = 0; frame < 6; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var foodBefore = source.Contents.Counts.GetValueOrDefault(ItemIds.Food);
            var sawDoorApproach = false;
            for (var tick = 0; tick < 500 && worker.Needs.IsHungry; tick++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                _woodHauling.ReconcileSchedule(worker, phase);
                SyncHouseDoorNavigation();
                AdvanceNavigationTick(.1);
                _foodConsumption.Advance(worker, species, tick);
                sawDoorApproach |= worker.Activity == "Opening home door for food";
                SyncEntityViews();
            }
            Require(sawDoorApproach && !worker.Needs.IsHungry && !house.IsClosed && !house.IsLocked
                && !HouseGeometry.Contains(house, worker.Position)
                && source.Contents.Counts.GetValueOrDefault(ItemIds.Food) == foodBefore - 1,
                $"hungry resident unlocks, crosses its door and eats during phase {phase}: {worker.Activity}, {worker.WaitingReason}");
            Require(_world.GetEmployment(worker.Id)?.Id == employmentId, "food detour retains the ongoing job");

            _world.TrySetEntityPosition(worker.Id, house.ToWorld(new(0, 0, 2.6f)));
            _woodHauling.ReconcileSchedule(worker, 60);
            Require(house.IsClosed && house.IsLocked, "resident closes and locks behind itself for rest");
            SyncHouseDoorNavigation();
            SyncEntityViews();
            _playerCharacter.GlobalPosition = ToGodotDoorPoint(house.ToWorld(new(0, 0, 5.1f)));
            SyncPlayerPosition();
            _selectedTarget = new(TargetKind.Structure, house.Id);
            PerformFriendlyInteraction();
            Require(house.IsLocked && house.IsClosed && _lastActionMessage.Contains("locked", StringComparison.OrdinalIgnoreCase),
                "F interaction cannot open another resident's locked door from outside");
            _playerCharacter.GlobalPosition = ToGodotDoorPoint(house.ToWorld(new(0, 0, 2.6f)));
            SyncPlayerPosition();
            PerformFriendlyInteraction();
            Require(!house.IsClosed && !house.IsLocked, "a visitor already inside can use the latch to leave");
            _playerCharacter.GlobalPosition = new(0, 0, -10);
            SyncPlayerPosition();
        }

        _woodHauling!.Reset();
        var stockpile = _world.Stockpiles.Single();
        stockpile.Contents.TryAdd(ItemIds.Wood, stockpile.Contents.Capacity);
        Require(ResourceTransferService.TryPickUp(_world.ResourcePiles.Single().Contents, worker, ItemIds.Wood, 5)
            == ResourceMutationResult.Success, "delivery fixture transfers its cargo from an actual source");
        _world.TrySetEntityPosition(worker.Id, house.ToWorld(new(0, 0, 8)));
        SyncEntityViews();
        Require(_woodHauling.ReconcileSchedule(worker, 60) && worker.Actions.Current?.Kind == ActionKind.Wander,
            "full storage sends the loaded resident home");
        stockpile.Contents.TryRemove(ItemIds.Wood, 5);
        Require(_woodHauling.Advance(worker, species, 0, 60).Status == WoodHaulingStatus.TravelingToStockpile,
            "new storage capacity cancels home travel and immediately starts delivery");
        for (var tick = 1; tick < 600 && worker.Inventory.GetValueOrDefault(ItemIds.Wood) > 0; tick++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            _woodHauling.ReconcileSchedule(worker, 60);
            SyncHouseDoorNavigation();
            AdvanceNavigationTick(.1);
            _woodHauling.Advance(worker, species, tick, 60);
            SyncEntityViews();
        }
        Require(worker.Inventory.GetValueOrDefault(ItemIds.Wood) == 0 && stockpile.Contents.Counts[ItemIds.Wood] == 100,
            $"resumed off-shift delivery reaches storage through live navigation: {worker.Activity}, {worker.WaitingReason}");

        // Commands must also be able to leave a home locked during the preceding rest.
        _woodHauling.Reset();
        _world.TrySetEntityPosition(worker.Id, house.ToWorld(new(0, 0, 2.6f)));
        Require(_world.OperateDoor(worker.Id, house.Id, HouseDoorAction.Lock).Succeeded, "resident secures door before command test");
        _world.TrySetEntityPosition(worker.Id, house.Position);
        _playerCharacter.GlobalPosition = ToGodotDoorPoint(house.ToWorld(new(0, 0, 12)));
        SyncPlayerPosition();
        SyncEntityViews();
        SyncHouseDoorNavigation();
        _world.AddSquadMember(worker.Id);
        var commandGoal = house.ToWorld(new NumericsVector3(0, 0, 8));
        Require(_squad!.Execute(SquadOrderKind.MoveTo, commandGoal).Code == SquadResult.Accepted,
            "squad movement is accepted for the resident");
        for (var tick = 0; tick < 350 && NumericsVector3.Distance(worker.Position, commandGoal) > .6f; tick++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            _squad.Advance();
            SyncHouseDoorNavigation();
            AdvanceNavigationTick(.1);
            SyncEntityViews();
        }
        Require(!house.IsClosed && !house.IsLocked && NumericsVector3.Distance(worker.Position, commandGoal) < .65f
            && _squad.Order == SquadOrderKind.MoveTo,
            $"squad movement opens the owner's door and continues its original destination: {worker.Position}, {worker.Activity}, "
                + $"{worker.WaitingReason}, {_entityViews[worker.Id].NavigationOutcome}, closed={house.IsClosed}, locked={house.IsLocked}");
        _squad.Reset();
        _world.RemoveSquadMember(worker.Id);
        _world.TrySetEntityPosition(worker.Id, house.ToWorld(new(0, 0, 2.6f)));
        Require(_world.OperateDoor(worker.Id, house.Id, HouseDoorAction.Lock).Succeeded, "resident secures door before follow test");
        _world.TrySetEntityPosition(worker.Id, house.Position);
        SyncEntityViews();
        SyncHouseDoorNavigation();
        _world.ApplyRelationshipAdjustment(worker.Id, _world.PlayerEntityId!, new RelationshipAdjustment(0, 0, 0, 80));
        IssueIndividualOrder(worker.Id, "follow");
        Require(worker.OrderIntent?.Kind == EntityOrderKind.Follow, "follow intent is issued through the regular command control");
        var followIntent = worker.OrderIntent;
        for (var tick = 0; tick < 350 && (HouseGeometry.Contains(house, worker.Position)
            || NumericsVector3.Distance(worker.Position, new(_playerCharacter.GlobalPosition.X, 0, _playerCharacter.GlobalPosition.Z)) > 2.1f); tick++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            _orders!.Advance();
            SyncHouseDoorNavigation();
            AdvanceNavigationTick(.1);
            SyncEntityViews();
        }
        Require(!house.IsClosed && !house.IsLocked && !HouseGeometry.Contains(house, worker.Position)
            && worker.OrderIntent == followIntent, "Follow unlocks the owned door and preserves its issuer intent");
    }

    private static Vector3 ToGodotDoorPoint(NumericsVector3 point) => new(point.X, point.Y, point.Z);
}
