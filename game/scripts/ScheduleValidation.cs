using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private async void RunScheduleValidation()
    {
        try
        {
            StartSession(PlayerCharacterProfile.Create("Schedule validation"), 42, "Schedule validation session.");
            _clock.SetPaused(true);
            _playerCharacter.SetPhysicsProcess(false);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var npc = _world.Entities.First(e => e.SpeciesId == "human" && e.Id != _world.PlayerEntityId);
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
            var beforeRest = npc.Position;
            _clock.SetPaused(false);
            _clock.Advance(0.1, AdvanceSimulationTick);
            _clock.SetPaused(true);
            Require(npc.Actions.Current is null && npc.Activity == "Resting", "rest cancels pickup on boundary");
            Require(npc.Position == beforeRest, "boundary cancels before navigation moves");
            Require(_entityViews[npc.Id].NavigationAdapter is GodotNavigationAdapter { HasActiveRequest: false }, "rest releases real navigation");
            Require(WoodTotal() == 100, "cancel preserves wood");
            _clock.Restore(240);
            // Hold the clock at a working phase while real navigation crosses physics frames.
            for (var tick = 0; tick < 600 && npc.Inventory.GetValueOrDefault(ItemIds.Wood) == 0; tick++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                AdvanceSimulationTick(0.1);
                SyncEntityViews();
            }
            Require(npc.Inventory.GetValueOrDefault(ItemIds.Wood) > 0, "next day resumes and picks up through navigation");
            _clock.Restore(340);
            for (var tick = 0; tick < 600 && npc.Inventory.GetValueOrDefault(ItemIds.Wood) > 0; tick++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                AdvanceSimulationTick(0.1);
                SyncEntityViews();
            }
            Require(npc.Inventory.GetValueOrDefault(ItemIds.Wood) == 0, "carried delivery finishes during rest");
            AdvanceSimulationTick(0.1);
            Require(npc.Activity == "Resting" && npc.Actions.Current is null, "no new pickup after delivery");
            Require(WoodTotal() == 100, "delivery conserves wood");
            _workStart.Value = 22;
            _workEnd.Value = 6;
            _applySchedule.EmitSignal(BaseButton.SignalName.Pressed);
            var json = WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime);
            var time = WorldSnapshotSerializer.Load(_world, json, _speciesCatalog, _foodConsumption!, _wandering!, _woodHauling!, _wander!, _squad!);
            _clock.Restore(time);
            RebuildPresentationFromWorld();
            SelectEntity(npc.Id);
            UpdateStatus();
            Require(_workStart.Value == 22 && _workEnd.Value == 6, "loaded hours populate controls");
            Require(_inspectorLabel.Text.Contains("Schedule: Rest | Next: 22:00 in 40.0s"), "loaded phase projection");
            Require(WorldSnapshotSerializer.Serialize(_world, time) == json, "durable round trip");
            var screenshotArgument = OS.GetCmdlineUserArgs().FirstOrDefault(argument => argument.StartsWith("--schedule-screenshot=", StringComparison.Ordinal));
            if (screenshotArgument is not null)
            {
                var output = screenshotArgument["--schedule-screenshot=".Length..];
                GetNode<ScrollContainer>("Controls/Panel/Scroll").ScrollVertical = 370;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng(output) == Error.Ok, "schedule screenshot");
                GetNode<ScrollContainer>("Controls/Panel/Scroll").ScrollVertical = int.MaxValue;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(Path.GetDirectoryName(output)!, "schedule-inspector.png")) == Error.Ok, "inspector screenshot");
            }
            GD.Print("SCHEDULE VALIDATION PASSED: inspector edits/rejection, boundary cancellation, next-day pickup, rest delivery, schema 10 round trip, wood 100.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"SCHEDULE VALIDATION FAILED: {exception}");
            GetTree().Quit(1);
        }
    }
}
