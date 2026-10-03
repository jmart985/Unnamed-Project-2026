using Godot;
using Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace UnnamedProject2026;

// Opt-in engine integration scenario. Uses the same button handlers and real navigation as play.
public partial class Main
{
    private void RunSquadValidation() => RunGameValidation(false);
    private void RunFirstGameValidation() => RunGameValidation(true);

    private async void RunGameValidation(bool firstGame)
    {
        try
        {
            StartSession(PlayerCharacterProfile.Create("Squad validation"), 42, "Squad validation session.");
            _clock.SetPaused(true);
            _playerCharacter.SetPhysicsProcess(false);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (firstGame) await ValidatePlayerLoop();
            var ids = _world.Entities.Where(e => e.SpeciesId == "human" && e.Id != _world.PlayerEntityId).Take(2).Select(e => e.Id).ToArray();
            foreach (var id in ids)
            {
                SelectEntity(id);
                PressSquadButton("Add selected");
            }
            Require(_world.SquadMemberIds.Count == 2, "two members added by UI");
            PressSquadButton("Add selected");
            Require(_world.SquadMemberIds.Count == 2, "membership does not duplicate");
            _squadX.Value = 4;
            _squadZ.Value = 3;
            PressSquadButton("Move to");
            PressSquadButton("Move to");
            await MoveSquadTo(new NumericsVector3(4, 0, 3), ids);
            Require(_inspectorLabel.Text.Contains("Squad member: True"), "inspector membership");
            Require(_inspectorLabel.Text.Contains("Squad arrived"), "inspector arrival");
            _playerCharacter.GlobalPosition = new Vector3(7, 0, -3);
            SyncPlayerPosition();
            PressSquadButton("Follow player");
            await MoveSquadTo(new NumericsVector3(7, 0, -3), ids);
            _playerCharacter.GlobalPosition = new Vector3(10, 0, -3);
            SyncPlayerPosition();
            await MoveSquadTo(new NumericsVector3(10, 0, -3), ids);
            PressSquadButton("Stop");
            AdvanceSimulationTick(0.1);
            var stopped = ids.Select(id => _world.Entities.Single(e => e.Id == id).Position).ToArray();
            for (var i = 0; i < 10; i++) AdvanceSimulationTick(0.1);
            Require(stopped.SequenceEqual(ids.Select(id => _world.Entities.Single(e => e.Id == id).Position)), "stop holds positions");
            Require(ids.All(id => _entityViews[id].NavigationAdapter is GodotNavigationAdapter { HasActiveRequest: false }), "stop cancels navigation");
            if (firstGame)
            {
                var beforeInvalidOrder = WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime);
                Require(_squad!.Execute(SquadOrderKind.MoveTo, new System.Numerics.Vector3(float.NaN, 0, 0)).Code == SquadResult.InvalidTarget, "invalid squad target rejection");
                Require(_squad.Order == SquadOrderKind.Stop && WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime) == beforeInvalidOrder, "invalid order preserves state");
            }
            // Avoid the player's save slot; exercise the same serializer/services/presentation path in memory.
            var json = WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime);
            PressSquadButton("Follow player");
            if (firstGame)
            {
                var invalid = System.Text.Json.Nodes.JsonNode.Parse(json)!;
                invalid["SquadMemberIds"] = new System.Text.Json.Nodes.JsonArray("missing");
                try
                {
                    WorldSnapshotSerializer.Load(_world, invalid.ToJsonString(), _speciesCatalog, _squad!);
                    throw new InvalidOperationException("invalid snapshot accepted");
                }
                catch (InvalidDataException) { }
                Require(WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime) == json, "invalid load preserves World");
                // Rebuild from a genuinely different new session before restoring the saved state.
                StartSession(PlayerCharacterProfile.Create("Reset session"), 43, "Reset before load.");
                _clock.SetPaused(true);
                _playerCharacter.SetPhysicsProcess(false);
            }
            var time = WorldSnapshotSerializer.Load(_world, json, _speciesCatalog, _foodConsumption!, _wandering!, _woodHauling!, _wander!, _squad!);
            _clock.Restore(time);
            RebuildPresentationFromWorld();
            SelectEntity(ids[0]);
            _lastActionMessage = "Loaded squad membership; movement orders reset.";
            UpdateStatus();
            Require(_world.SquadMemberIds.SequenceEqual(ids.Order(StringComparer.Ordinal)), "membership round trip");
            Require(_squad!.Order == SquadOrderKind.None, "orders clear on load");
            Require(_inspectorLabel.Text.Contains("Squad member: True | Order: None"), "loaded inspector");
            if (firstGame)
            {
                Require(WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime) == json, "full durable round trip");
                Require(_world.Entities.All(entity => entity.Actions.Current is null), "no stale actions after load");
                Require(FoodTotal() == 399 && WoodTotal() == 100, "food/wood conservation after load");
                GD.Print("FIRST GAME VALIDATION PASSED: player movement/eating, squad orders, directed greeting, rejection, reset/load, food 399, wood 100.");
            }
            var screenshotArgument = OS.GetCmdlineUserArgs().FirstOrDefault(argument => argument.StartsWith("--squad-screenshot=", StringComparison.Ordinal));
            if (screenshotArgument is not null)
            {
                GetNode<ScrollContainer>("Controls/Panel/Scroll").ScrollVertical = 350;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var error = GetViewport().GetTexture().GetImage().SavePng(screenshotArgument["--squad-screenshot=".Length..]);
                Require(error == Error.Ok, $"screenshot: {error}");
                GetNode<ScrollContainer>("Controls/Panel/Scroll").ScrollVertical = int.MaxValue;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var inspectorPath = Path.Combine(Path.GetDirectoryName(screenshotArgument["--squad-screenshot=".Length..])!, "squad-inspector.png");
                Require(GetViewport().GetTexture().GetImage().SavePng(inspectorPath) == Error.Ok, "inspector screenshot");
            }
            if (!firstGame) GD.Print($"SQUAD VALIDATION PASSED: {string.Join(", ", ids)}; move/follow/stop, authoritative positions, inspector, schema 9 load.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"{(firstGame ? "FIRST GAME" : "SQUAD")} VALIDATION FAILED: {exception}");
            GetTree().Quit(1);
        }
    }

    private void PressSquadButton(string text)
    {
        var controls = GetNode<VBoxContainer>("Controls/Panel/Scroll/Margin/VBox");
        var button = controls.GetChildren().OfType<HBoxContainer>().SelectMany(row => row.GetChildren().OfType<Button>()).Single(button => button.Text == text);
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private async Task MoveSquadTo(NumericsVector3 target, string[] ids)
    {
        for (var tick = 0; tick < 600; tick++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            AdvanceSimulationTick(0.1);
            SyncEntityViews();
            UpdateStatus();
            if (ids.All(id => _world.TryGetEntity(id, out var entity) && entity is not null
                && NumericsVector3.DistanceSquared(entity.Position, target) <= 0.25f)) return;
        }
        throw new InvalidOperationException($"Squad did not reach {target}: " + string.Join("; ", ids.Select(id => $"{id} {_world.Entities.Single(e => e.Id == id).Position} {_entityViews[id].NavigationOutcome}")));
    }

    private static void Require(bool condition, string assertion)
    {
        if (!condition) throw new InvalidOperationException(assertion);
    }
}
