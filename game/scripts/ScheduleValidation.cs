using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private async void RunScheduleValidation()
    {
        try
        {
            var integrated = OS.GetCmdlineUserArgs().Contains("--validate-schedule-integration");
            StartSession(PlayerCharacterProfile.Create("Schedule validation"), 42, "Schedule validation session.");
            Require(_world.ResourcePiles.Count == 1 && WoodTotal() == 100,
                "schedule validation uses the baseline resource profile");
            _clock.SetPaused(true);
            _playerCharacter.SetPhysicsProcess(false);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var npc = _world.Entities.First(e => e.SpeciesId == "human" && e.Id != _world.PlayerEntityId);
            Require(_world.TryGetHomePosition(npc.Id, out var home), "boundary fixture NPC has a home");
            SelectEntity(npc.Id);
            Require(_workStart.Value == 8 && _workEnd.Value == 18, "selected hours reflect World");
            Require(_inspectorLabel.Text.Contains("Schedule: Work | Next: 18:00 in 100.0s"), "default phase projection");
            _workStart.Value = 9;
            _workEnd.Value = 9;
            _applySchedule.EmitSignal(BaseButton.SignalName.Pressed);
            Require(_world.GetWorkSchedule(npc.Id) == NpcWorkSchedule.Default, "invalid hours preserve World");
            Require(_lastActionMessage.Contains("different hours"), "invalid hours feedback");
            _workStart.Value = 8;
            _workEnd.Value = 18;
            _applySchedule.EmitSignal(BaseButton.SignalName.Pressed);
            _clock.Restore(99.9);
            AdvanceSimulationTick(0.1);
            Require(npc.Actions.Current?.Kind == ActionKind.Haul && npc.Activity == "SeekingWood", "real pickup begins during work");
            var pickupRequestId = (_entityViews[npc.Id].NavigationAdapter as GodotNavigationAdapter)?.ActiveRequestId;
            Require(pickupRequestId is not null, "work pickup owns a real navigation request");
            _clock.SetPaused(false);
            _clock.Advance(0.1, AdvanceSimulationTick);
            _clock.SetPaused(true);
            var homeNavigator = _entityViews[npc.Id].NavigationAdapter as GodotNavigationAdapter;
            Require(npc.Actions.Current?.Kind == ActionKind.Wander && npc.Activity == "Going home",
                "rest cancels work and starts the existing home-return behavior");
            Require(homeNavigator is { HasActiveRequest: true }
                && homeNavigator.ActiveRequestId != pickupRequestId
                && _entityViews[npc.Id].NavigationTarget == home,
                "rest replaces the pickup request with a home navigation request");
            Require(WoodTotal() == 100, "cancel preserves wood");
            // Keep the next-shift pickup fixture clear of the crowded spawn. The
            // boundary above already verifies real home navigation; this step
            // isolates schedule resumption while retaining a real short pickup path.
            var woodSource = _world.ResourcePiles.Single(pile => pile.Contents.Counts.GetValueOrDefault(ItemIds.Wood) > 0);
            Require(_world.TrySetEntityPosition(npc.Id, woodSource.Position - new System.Numerics.Vector3(2, 0, 0)),
                "next-shift fixture places NPC beside the wood source");
            SyncEntityViews();
            _clock.Restore(240);
            // Hold the clock at a working phase while real navigation crosses physics frames.
            for (var tick = 0; tick < 600 && npc.Inventory.GetValueOrDefault(ItemIds.Wood) == 0; tick++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                AdvanceSimulationTick(0.1);
                SyncEntityViews();
            }
            Require(npc.Inventory.GetValueOrDefault(ItemIds.Wood) > 0,
                $"next day resumes and picks up through navigation (activity {npc.Activity}; action {npc.Actions.Current?.Kind}; position {npc.Position}; wait {npc.WaitingReason}; navigation {_entityViews[npc.Id].NavigationOutcome}; target {_entityViews[npc.Id].NavigationTarget})");
            if (integrated)
            {
                var cargo = npc.Inventory[ItemIds.Wood];
                var carrying = WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime);
                var bad = System.Text.Json.Nodes.JsonNode.Parse(carrying)!;
                bad["WorkSchedules"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
                {
                    ["EntityId"] = npc.Id, ["StartHour"] = 6, ["EndHour"] = 6
                });
                try
                {
                    WorldSnapshotSerializer.Load(_world, bad.ToJsonString(), _speciesCatalog, _foodConsumption!, _woodHauling!, _squad!);
                    throw new InvalidOperationException("invalid schedule accepted");
                }
                catch (InvalidDataException) { }
                Require(WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime) == carrying, "invalid hours preserve carried snapshot");
                Require(_entityViews[npc.Id].NavigationAdapter is GodotNavigationAdapter { HasActiveRequest: true }, "invalid load preserves delivery navigation");
                var carryingTime = WorldSnapshotSerializer.Load(_world, carrying, _speciesCatalog, _foodConsumption!, _wandering!, _woodHauling!, _squad!);
                _clock.Restore(carryingTime);
                Require(_entityViews[npc.Id].NavigationAdapter is GodotNavigationAdapter { HasActiveRequest: false }, "valid load cancels old delivery navigation");
                var npcId = npc.Id;
                RebuildPresentationFromWorld();
                npc = _world.Entities.Single(entity => entity.Id == npcId);
                SelectEntity(npc.Id);
                Require(npc.Inventory[ItemIds.Wood] == cargo && npc.Actions.Current is null, "carried inventory survives load without stale action");
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            // The rest delivery uses a short real path near the stockpile so the
            // assertion measures schedule behavior, not the crowded town crossing.
            var stockpile = _world.Stockpiles.Single();
            Require(_world.TrySetEntityPosition(npc.Id, stockpile.Position + new System.Numerics.Vector3(0, 0, -2)),
                "rest-delivery fixture places the carrier beside the stockpile");
            SyncEntityViews();
            _clock.Restore(340);
            for (var tick = 0; tick < 600 && npc.Inventory.GetValueOrDefault(ItemIds.Wood) > 0; tick++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                AdvanceSimulationTick(0.1);
                SyncEntityViews();
            }
            Require(npc.Inventory.GetValueOrDefault(ItemIds.Wood) == 0,
                $"carried delivery finishes during rest (activity {npc.Activity}; action {npc.Actions.Current?.Kind}; position {npc.Position}; wait {npc.WaitingReason}; navigation {_entityViews[npc.Id].NavigationOutcome}; target {_entityViews[npc.Id].NavigationTarget})");
            AdvanceSimulationTick(0.1);
            Require(!_world.GetWorkSchedule(npc.Id).Evaluate(_clock.SimulationTime).IsWorking
                && npc.Actions.Current?.Kind != ActionKind.Haul && npc.Activity == "Going home",
                "after delivery, the off-shift worker returns home without another pickup");
            Require(WoodTotal() == 100, "delivery conserves wood");
            _workStart.Value = 22;
            _workEnd.Value = 6;
            _applySchedule.EmitSignal(BaseButton.SignalName.Pressed);
            var json = WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime);
            var time = WorldSnapshotSerializer.Load(_world, json, _speciesCatalog, _foodConsumption!, _wandering!, _woodHauling!, _squad!);
            _clock.Restore(time);
            RebuildPresentationFromWorld();
            SelectEntity(npc.Id);
            UpdateStatus();
            Require(_workStart.Value == 22 && _workEnd.Value == 6, "loaded hours populate controls");
            Require(_inspectorLabel.Text.Contains("Schedule: Rest | Next: 22:00 in 40.0s"), "loaded phase projection");
            Require(WorldSnapshotSerializer.Serialize(_world, time) == json, "durable round trip");
            if (integrated) await ValidateScheduleOverrides(npc.Id);
            var screenshotArgument = OS.GetCmdlineUserArgs().FirstOrDefault(argument => argument.StartsWith("--schedule-screenshot=", StringComparison.Ordinal));
            if (screenshotArgument is not null)
            {
                if (_menuState.ActiveMenu != PlayerMenu.Settlement) ToggleMenu(PlayerMenu.Settlement);
                _settlementTabs.CurrentTab = 1;
                var output = screenshotArgument["--schedule-screenshot=".Length..];
                GetNode<ScrollContainer>("Controls/Panel/Scroll").ScrollVertical = 370;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng(output) == Error.Ok, "schedule screenshot");
                GetNode<ScrollContainer>("Controls/Panel/Scroll").ScrollVertical = int.MaxValue;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(Path.GetDirectoryName(output)!, "schedule-inspector.png")) == Error.Ok, "inspector screenshot");
            }
            GD.Print("SCHEDULE VALIDATION PASSED: inspector edits/rejection, boundary cancellation, next-day pickup, rest delivery, schema 10 round trip, wood 100.");
            if (integrated)
            {
                await ValidateNaturalDayLoop();
                GD.Print("SCHEDULE INTEGRATION PASSED: carried-load recovery, hunger/squad overrides during rest, natural day progression, pause/4x, food conservation, wood 100.");
            }
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"SCHEDULE VALIDATION FAILED: {exception}");
            GetTree().Quit(1);
        }
    }
}
