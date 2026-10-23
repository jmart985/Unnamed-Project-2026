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
            StartSession(PlayerCharacterProfile.Create("Squad validation"), 42, "Squad validation session.", survivalGathering: firstGame);
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
            await MoveSquadTo(new NumericsVector3(7, 0, -3), ids, following: true);
            _playerCharacter.GlobalPosition = new Vector3(10, 0, -3);
            SyncPlayerPosition();
            await MoveSquadTo(new NumericsVector3(10, 0, -3), ids, following: true);
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
            var time = WorldSnapshotSerializer.Load(_world, json, _speciesCatalog, _foodConsumption!, _wandering!, _woodHauling!, _squad!);
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
                Require(FoodTotal() == 399 && WoodTotal() == 48, "food/wood conservation after load");
                GD.Print("FIRST GAME VALIDATION PASSED: player movement/eating, squad orders, directed greeting, rejection, reset/load, food 399, wood 48 from four trees.");
            }
            var screenshotArgument = OS.GetCmdlineUserArgs().FirstOrDefault(argument => argument.StartsWith("--squad-screenshot=", StringComparison.Ordinal));
            if (screenshotArgument is not null)
            {
                if (_menuState.ActiveMenu != PlayerMenu.Settlement) ToggleMenu(PlayerMenu.Settlement);
                _settlementTabs.CurrentTab = 2;
                GetNode<ScrollContainer>("Controls/Panel/Scroll").ScrollVertical = 350;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var error = GetViewport().GetTexture().GetImage().SavePng(screenshotArgument["--squad-screenshot=".Length..]);
                Require(error == Error.Ok, $"screenshot: {error}");
                GetNode<ScrollContainer>("Controls/Panel/Scroll").ScrollVertical = int.MaxValue;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var inspectorPath = Path.Combine(Path.GetDirectoryName(screenshotArgument["--squad-screenshot=".Length..])!, "squad-inspector.png");
                Require(GetViewport().GetTexture().GetImage().SavePng(inspectorPath) == Error.Ok, "inspector screenshot");
            }
            if (!firstGame) GD.Print($"SQUAD VALIDATION PASSED: {string.Join(", ", ids)}; move/follow/stop, authoritative positions, inspector, schema {WorldSnapshotSerializer.CurrentSchemaVersion} load.");
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
        var button = controls.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == text);
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private async Task MoveSquadTo(NumericsVector3 target, string[] ids, bool following = false)
    {
        for (var tick = 0; tick < 600; tick++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            AdvanceSimulationTick(0.1);
            SyncEntityViews();
            UpdateStatus();
            if (ids.All(id => _world.TryGetEntity(id, out var entity) && entity is not null
                && (following
                    ? NumericsVector3.DistanceSquared(entity.Position, target) <= SquadOrderService.FollowResumeDistance * SquadOrderService.FollowResumeDistance
                        && entity.Actions.Current?.Kind != ActionKind.SquadMove
                    : _entityViews[id].NavigationTarget is { } assignedTarget
                        && NumericsVector3.DistanceSquared(entity.Position, assignedTarget) <= 0.25f
                        && NumericsVector3.DistanceSquared(assignedTarget, target) <= 1.0f
                        && entity.Actions.Current?.Kind != ActionKind.SquadMove))) return;
        }
        throw new InvalidOperationException($"Squad did not reach {target}: " + string.Join("; ", ids.Select(id => $"{id} {_world.Entities.Single(e => e.Id == id).Position} {_entityViews[id].NavigationOutcome}")));
    }

    private async void RunFollowSpacingValidation()
    {
        try
        {
            StartSession(PlayerCharacterProfile.Create("Follow spacing"), 42, "Follow spacing validation.");
            SetProcess(false); // Explicit bounded ticks; player physics and actual navigation remain enabled.
            SetProcessInput(false);
            SetProcessUnhandledInput(false); // Fixture commands use existing UI handlers, not desktop input.
            _playerCharacter.SetProcessUnhandledInput(false);
            _clock.SetPaused(true);
            _playerCharacter.SetPhysicsProcess(true);
            _playerCharacter.SetGameplayInputEnabled(false);
            _playerCharacter.GlobalPosition = new Vector3(0, 0, -6);
            _playerCharacter.Velocity = Vector3.Zero;
            var members = _world.Entities.Where(e => e.SpeciesId == "human" && e.Id != _world.PlayerEntityId).ToArray();
            var ids = members.Select(e => e.Id).ToArray();
            foreach (var entity in _world.Entities)
            {
                entity.Actions.Cancel();
                _world.TrySetEntityPosition(entity.Id, new(40, 0, 40));
            }
            for (var i = 0; i < members.Length; i++)
            {
                var angle = Mathf.Tau * i / members.Length;
                _world.TrySetEntityPosition(members[i].Id, new(6 * Mathf.Cos(angle), 0, -6 + 6 * Mathf.Sin(angle)));
                members[i].Needs.AddNutrition(100);
            }
            SyncPlayerPosition(); SyncEntityViews();
            var camera = GetNode<Camera3D>("Camera3D"); camera.Current = true;
            camera.GlobalPosition = new(0, 6, 0); camera.LookAt(new Vector3(0, 0.8f, -6));
            for (var frame = 0; frame < 10; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var initial = _playerCharacter.GlobalPosition;
            SelectEntity(ids[0]); PressSquadButton("Add selected"); PressSquadButton("Follow player");
            await MoveSquadTo(new NumericsVector3(initial.X, initial.Y, initial.Z), [ids[0]], following: true);
            Require(HorizontalDistance(_entityViews[ids[0]].GlobalPosition, _playerCharacter.GlobalPosition) >= 1.999f, "single follower safe stop distance");
            // Give this follower a real active path, then approach it before its next movement tick.
            var firstPosition = _entityViews[ids[0]].GlobalPosition;
            _playerCharacter.GlobalPosition = firstPosition + new Vector3(5, 0, 0);
            SyncPlayerPosition(); _squad!.Advance();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            _playerCharacter.GlobalPosition = firstPosition + new Vector3(1.2f, 0, 0);
            var approached = _playerCharacter.GlobalPosition;
            AdvanceNavigationTick(1); // Large step on the stale path, through the actual adapter.
            Require(_entityViews[ids[0]].GlobalPosition == firstPosition, "approaching player halts stale follow step before collider moves");
            _squad.Advance();
            for (var frame = 0; frame < 10; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Require(HorizontalDistance(approached, _playerCharacter.GlobalPosition) < 0.01f, "approached follower does not push player");
            _playerCharacter.GlobalPosition = initial; SyncPlayerPosition();
            for (var i = 1; i < ids.Length; i++) { SelectEntity(ids[i]); PressSquadButton("Add selected"); }
            await MoveSquadTo(new NumericsVector3(initial.X, initial.Y, initial.Z), ids, following: true);
            for (var tick = 0; tick < 200; tick++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                AdvanceSimulationTick(0.1); SyncEntityViews();
                Require(HorizontalDistance(initial, _playerCharacter.GlobalPosition) < 0.01f, "stationary player is not pushed");
                Require(members.All(e => HorizontalDistance(_entityViews[e.Id].GlobalPosition, _playerCharacter.GlobalPosition) >= 1.999f), "multiple followers stay outside player boundary");
            }
            var held = members.Select(e => e.Position).ToArray();
            for (var tick = 0; tick < 40; tick++) { AdvanceSimulationTick(0.1); SyncEntityViews(); }
            Require(held.SequenceEqual(members.Select(e => e.Position)), "holding band does not creep");
            SelectEntity(VisibleWorldTargets().First(candidate => candidate.Target.Kind == TargetKind.Entity && ids.Contains(candidate.Target.Id)).Target.Id);
            ClosePlayerMenus();
            UpdateStatus();
            await CaptureGameplayRefinement("follow-spacing.png");
            // Move toward a follower while a stale path exists; movement must use live player coordinates.
            _playerCharacter.GlobalPosition += new Vector3(5, 0, -4);
            _playerCharacter.Velocity = Vector3.Zero;
            var moved = _playerCharacter.GlobalPosition;
            await MoveSquadTo(new(moved.X, moved.Y, moved.Z), ids, following: true);
            Require(members.Any(e => NumericsVector3.DistanceSquared(e.Position, held[Array.IndexOf(members, e)]) > 1), "following resumes after leader moves");
            for (var tick = 0; tick < 80; tick++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                AdvanceSimulationTick(0.1); SyncEntityViews();
                Require(HorizontalDistance(moved, _playerCharacter.GlobalPosition) < 0.01f, "resumed squad does not push the player");
            }
            // Exercise the real 4x clock's capped multi-tick frame path near the new boundary.
            _clock.SetPaused(false); _clock.SetSpeed(SimulationSpeed.FourX);
            _playerCharacter.GlobalPosition += new Vector3(-5, 0, 0);
            for (var frame = 0; frame < 120; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                _clock.Advance(0.1, AdvanceSimulationTick); SyncEntityViews();
                Require(members.All(e => HorizontalDistance(_entityViews[e.Id].GlobalPosition, _playerCharacter.GlobalPosition) >= 1.999f), "4x bounded steps keep clearance");
            }
            PressSquadButton("Stop"); AdvanceSimulationTick(0.1);
            Require(members.All(e => e.Actions.Current?.Kind != ActionKind.SquadMove), "stop cancels all follow requests");
            GD.Print($"FOLLOW SPACING VALIDATION PASSED: {ids.Length} followers; single/multiple safe arrival, stationary player <0.01 m drift, holding, resumed follow, live boundary and 4x steps.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"FOLLOW SPACING VALIDATION FAILED: {exception}");
            GetTree().Quit(1);
        }
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
        => new Vector2(first.X - second.X, first.Z - second.Z).Length();

    private static void Require(bool condition, string assertion)
    {
        if (!condition) throw new InvalidOperationException(assertion);
    }
}
