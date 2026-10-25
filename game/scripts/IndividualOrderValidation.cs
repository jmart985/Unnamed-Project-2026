using Godot;
using Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace UnnamedProject2026;

public partial class Main
{
    private async void RunIndividualOrderValidation()
    {
        try
        {
            StartSession(PlayerCharacterProfile.Create("Order validation"), 42, "Individual order validation.");
            SetProcess(false);
            SetProcessInput(false);
            SetProcessUnhandledInput(false);
            _playerCharacter.SetProcessUnhandledInput(false);
            _playerCharacter.SetPhysicsProcess(false);
            _clock.SetPaused(true);

            var player = _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId);
            var target = _world.Entities.First(entity => entity.SpeciesId == "human" && entity.Id != player.Id);
            var otherEntities = _world.Entities.Where(entity => entity.Id != player.Id && entity.Id != target.Id).ToArray();
            for (var index = 0; index < otherEntities.Length; index++)
            {
                otherEntities[index].Actions.Cancel();
                _world.TrySetEntityPosition(otherEntities[index].Id, new NumericsVector3(80 + index * 2, 0, 80));
            }
            _world.TrySetEntityPosition(player.Id, NumericsVector3.Zero);
            _playerCharacter.GlobalPosition = Vector3.Zero;
            _world.TrySetEntityPosition(target.Id, new NumericsVector3(8, 0, 0));
            target.Needs.AddNutrition(100);
            _world.ApplyRelationshipAdjustment(target.Id, player.Id, new RelationshipAdjustment(0, 0, 0, 80));
            SyncEntityViews();
            for (var frame = 0; frame < 20; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            ToggleMenu(PlayerMenu.Settlement);
            _settlementTabs.CurrentTab = 1;
            SelectEntity(target.Id);
            _followSelectedOrderButton.EmitSignal(BaseButton.SignalName.Pressed);
            Require(target.OrderIntent == new EntityOrder(EntityOrderKind.Follow, player.Id), "inspector Follow button records player issuer");
            await RunUntilFollowStops(target, player);
            Require(target.Actions.Current?.Kind == ActionKind.CommandMove, "Follow remains active while holding position");
            Require(_inspectorLabel.Text.Contains($"Individual order: Follow {player.Id}"), "inspector reports the individual order");

            var stayPosition = target.Position;
            _entityButtons[target.Id].EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Right,
                Pressed = true
            });
            Require(_entityContextMenu.Visible, "roster context menu opens for the selected entity");
            _entityContextMenu.EmitSignal(PopupMenu.SignalName.IdPressed, 2L);
            Require(target.OrderIntent == new EntityOrder(EntityOrderKind.Stay, player.Id, stayPosition), "context-menu Stay button records the current position");

            var away = stayPosition + new NumericsVector3(5, 0, 0);
            _world.TrySetEntityPosition(target.Id, away);
            SyncEntityViews();
            await RunUntilStayReturns(target, stayPosition);

            // Saving while the entity is away from its Stay anchor checks that the order,
            // rather than the live navigation path, is restored and starts a fresh route.
            _world.TrySetEntityPosition(target.Id, stayPosition + new NumericsVector3(5, 0, 0));
            SyncEntityViews();
            AdvanceSimulationTick(0.1);
            var snapshot = WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime);
            var targetId = target.Id;
            var time = WorldSnapshotSerializer.Load(_world, snapshot, _speciesCatalog,
                _foodConsumption!, _creatureHunting!, _wildPreyThreat!, _wandering!, _woodHauling!, _squad!, _orders!);
            _clock.Restore(time);
            RebuildPresentationFromWorld();
            target = _world.Entities.Single(entity => entity.Id == targetId);
            SelectEntity(target.Id);
            Require(target.OrderIntent is { Kind: EntityOrderKind.Stay }, "saved Stay order survives loading");
            Require(target.Actions.Current is null, "load does not restore an old movement action");
            await RunUntilStayReturns(target, stayPosition);
            GD.Print("INDIVIDUAL ORDER VALIDATION PASSED: inspector Follow, roster context-menu Stay, real navigation, and route rebuild after save/load.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"INDIVIDUAL ORDER VALIDATION FAILED: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task RunUntilFollowStops(EntityRecord target, EntityRecord issuer)
    {
        for (var tick = 0; tick < 800; tick++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            AdvanceSimulationTick(0.1);
            SyncEntityViews();
            var dx = target.Position.X - issuer.Position.X;
            var dz = target.Position.Z - issuer.Position.Z;
            if (MathF.Sqrt(dx * dx + dz * dz) <= SquadOrderService.FollowStopDistance + 0.01f
                && target.Actions.Current?.Kind == ActionKind.CommandMove
                && _entityViews[target.Id].NavigationAdapter is GodotNavigationAdapter { HasActiveRequest: false })
                return;
        }
        throw new InvalidOperationException($"Follow did not reach its stop radius: target {target.Position}, issuer {issuer.Position}, nav {_entityViews[target.Id].NavigationOutcome}.");
    }

    private async Task RunUntilStayReturns(EntityRecord target, NumericsVector3 position)
    {
        for (var tick = 0; tick < 800; tick++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            AdvanceSimulationTick(0.1);
            SyncEntityViews();
            if (NumericsVector3.DistanceSquared(target.Position, position) <= 0.25f
                && target.Actions.Current?.Kind == ActionKind.CommandMove
                && _entityViews[target.Id].NavigationAdapter is GodotNavigationAdapter { HasActiveRequest: false })
                return;
        }
        throw new InvalidOperationException($"Stay did not return to {position}: target {target.Position}, nav {_entityViews[target.Id].NavigationOutcome}.");
    }
}
