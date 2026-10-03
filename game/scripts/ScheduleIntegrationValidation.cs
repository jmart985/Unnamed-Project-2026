using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private async Task ValidateScheduleOverrides(string npcId)
    {
        var npc = _world.Entities.Single(entity => entity.Id == npcId);
        npc.Needs.Advance(200); // Explicit hunger fixture; scheduling must not block the existing food loop.
        var foodBefore = FoodTotal();
        AdvanceSimulationTick(0.1);
        Require(npc.Actions.Current?.Kind == ActionKind.Eat, "hunger starts eating during rest");
        for (var tick = 0; tick < 600 && npc.Needs.IsHungry; tick++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            AdvanceSimulationTick(0.1);
            SyncEntityViews();
        }
        Require(!npc.Needs.IsHungry && FoodTotal() < foodBefore, "resting worker reaches and consumes food");
        SelectEntity(npcId);
        PressSquadButton("Add selected");
        _squadX.Value = 4;
        _squadZ.Value = 3;
        PressSquadButton("Move to");
        AdvanceSimulationTick(0.1);
        Require(npc.Actions.Current?.Kind == ActionKind.SquadMove, "direct squad movement overrides rest");
        await MoveSquadTo(new System.Numerics.Vector3(4, 0, 3), [npcId]);
        Require(_inspectorLabel.Text.Contains("Schedule: Rest") && _inspectorLabel.Text.Contains("Squad arrived"), "inspector explains rest with squad arrival");
        PressSquadButton("Stop");
        PressSquadButton("Remove member");
        AdvanceSimulationTick(0.1);
        UpdateStatus();
        Require(npc.Activity == "Resting" && npc.Actions.Current is null, "returns to rest after squad removal");
        Require(WoodTotal() == 100, "overrides preserve wood");
    }

    private async Task ValidateNaturalDayLoop()
    {
        StartSession(PlayerCharacterProfile.Create("Natural day validation"), 42, "Natural day validation.");
        _clock.SetPaused(true);
        _playerCharacter.SetPhysicsProcess(false);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var npc = _world.Entities.First(entity => entity.SpeciesId == "human" && entity.Id != _world.PlayerEntityId);
        SelectEntity(npc.Id);
        var paused = WorldSnapshotSerializer.Serialize(_world, 0);
        _clock.Advance(1, AdvanceSimulationTick);
        Require(WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime) == paused, "pause freezes complete world and daily clock");
        GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/FourX").EmitSignal(BaseButton.SignalName.Pressed);
        var sawRest = false;
        while (_clock.SimulationTime < 240.4 - 1e-8)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var priorTime = _clock.SimulationTime;
            _clock.SetPaused(false);
            _clock.Advance(0.1, AdvanceSimulationTick);
            _clock.SetPaused(true);
            SyncEntityViews();
            UpdateStatus();
            Require(Math.Abs(_clock.SimulationTime - priorTime - 0.4) < 1e-7, "4x advances four fixed ticks");
            Require(WoodTotal() == 100, "natural clock conserves wood each frame");
            Require(FoodTotal() + _foodConsumption!.FoodUnitsConsumed == 400, "natural clock accounts for consumed food each frame");
            var phase = _world.GetWorkSchedule(npc.Id).Evaluate(_clock.SimulationTime);
            if (!phase.IsWorking)
            {
                sawRest = true;
                Require(npc.Actions.Current?.Kind != ActionKind.Haul || npc.Inventory.GetValueOrDefault(ItemIds.Wood) > 0,
                    "natural rest only allows carried hauling");
            }
        }
        Require(sawRest && _world.GetWorkSchedule(npc.Id).Evaluate(_clock.SimulationTime).IsWorking, "natural full day reaches rest then next work");
        Require(npc.Activity != "Resting", "work phase resumes after rest");
        Require(_foodConsumption!.FoodUnitsConsumed > 0, "natural day includes hunger response");
        GD.Print($"NATURAL DAY EVIDENCE: time {_clock.SimulationTime:0.0}s, food remaining {FoodTotal()}, consumed {_foodConsumption.FoodUnitsConsumed}, wood {WoodTotal()}.");
        var screenshot = OS.GetCmdlineUserArgs().FirstOrDefault(argument => argument.StartsWith("--day-screenshot=", StringComparison.Ordinal));
        if (screenshot is not null)
        {
            GetNode<ScrollContainer>("Controls/Panel/Scroll").ScrollVertical = 0;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Require(GetViewport().GetTexture().GetImage().SavePng(screenshot["--day-screenshot=".Length..]) == Error.Ok, "natural day screenshot");
        }
    }
}
