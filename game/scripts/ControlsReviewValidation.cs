using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private async Task ValidateReviewedControlRegressions()
    {
        StartSession(PlayerCharacterProfile.Create("Controls review"), 42, "Review regressions.");
        SetProcess(false);
        _clock.SetPaused(false);
        SynchronizeUnifiedContext();
        var preferences = _controlSettings.Preferences;
        Require(_controlSettings.SetPreferences(preferences with { ToggleWheels = true }, out _), "toggle fixture saves");
        SendMenuKey(Key.Q);
        Require(UnifiedWheelOpen, "toggle squad wheel opens");
        SendMenuKey(Key.Q);
        Require(!UnifiedWheelOpen, "same opener closes toggle squad wheel");
        SendMenuKey(Key.B);
        GetViewport().PushInput(new InputEventJoypadButton { Device = 7, ButtonIndex = JoyButton.RightShoulder, Pressed = true });
        GetViewport().PushInput(new InputEventJoypadButton { Device = 7, ButtonIndex = JoyButton.RightShoulder, Pressed = false });
        Require(UnifiedWheelOpen, "controller toggle build wheel opens");
        GetViewport().PushInput(new InputEventJoypadButton { Device = 7, ButtonIndex = JoyButton.RightShoulder, Pressed = true });
        GetViewport().PushInput(new InputEventJoypadButton { Device = 7, ButtonIndex = JoyButton.RightShoulder, Pressed = false });
        Require(!UnifiedWheelOpen && PlacementPanelOpen, "same controller opener closes only its build wheel");
        SendMenuKey(Key.B);
        _controlSettings.SetPreferences(preferences, out _);

        ToggleMenu(PlayerMenu.Settings);
        _settingsTabs.CurrentTab = 1;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _controlSearch.Text = "abcd";
        _controlSearch.GrabFocus();
        _controlSearch.CaretColumn = 4;
        SynchronizeUnifiedContext();
        foreach (var echo in new[] { false, true })
            GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.Backspace, Keycode = Key.Backspace, Pressed = true, Echo = echo });
        GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.Backspace, Keycode = Key.Backspace, Pressed = false });
        Require(_controlSearch.Text == "ab", "native search receives repeated Backspace: " + _controlSearch.Text
            + "; context=" + _inputRouter.Context + "; focus=" + GetViewport().GuiGetFocusOwner()?.Name);
        _controlSearch.Text = "";
        _controlSearch.ReleaseFocus();
        ClosePlayerMenus();

        var player = _world.Entities.Single(e => e.Id == _world.PlayerEntityId);
        SyncPlayerPosition();
        var hut = _world.AddStructure("control-review-hut", StructureType.Hut, player.Position);
        hut.AddBed(new Bed("control-review-bed"));
        Require(new SettlementRestService(_world).Sleep(player.Id, hut.Id).Succeeded, "sleep fixture uses rest service");
        SynchronizeUnifiedContext();
        var strikes = 0;
        void Strike(bool _, string __) => strikes++;
        _playerCharacter.StrikeReleased += Strike;
        try
        {
            SendMenuMouse(MouseButton.Left);
            SendMenuMouse(MouseButton.Right);
            _playerCharacter.PerformEquipmentAction(true);
            await PhysicsFrames(30);
            Require(strikes == 0 && player.SleepState == SleepState.Sleeping, "sleeping input and direct presentation entry cannot attack");
            SendMenuKey(Key.G);
            Require(UnifiedWheelOpen, "sleeping player can open wake choices");
            _controlWheel.Stick(Vector2.Up);
            SendControlAction(ControllerActions.MenuConfirm);
            Require(player.SleepState != SleepState.Sleeping && !UnifiedWheelOpen, "wake remains reachable through live controls");
        }
        finally { _playerCharacter.StrikeReleased -= Strike; }

        // Use real rendered bounds and viewport interaction, not a mocked selection helper.
        _playerCharacter.SetPhysicsProcess(false);
        var camera = GetNode<Camera3D>("Camera3D");
        camera.Current = true;
        var bench = new WorldTarget(TargetKind.Workstation, _world.Workstations.First().Id);
        var farm = new WorldTarget(TargetKind.Farm, _world.Farms.First().Id);
        void Aim(WorldTarget target)
        {
            var view = WorldTargets().Single(v => v.Target == target);
            _playerCharacter.GlobalPosition = view.Center + new Vector3(0, -view.Center.Y, -1.5f);
            SyncPlayerPosition();
            camera.GlobalPosition = view.Center + new Vector3(-2, 3, -4);
            camera.LookAt(view.Center);
        }
        ClearTargetLock();
        Aim(bench);
        SendMenuKey(Key.E);
        Require(_selectedTarget == bench && WorkstationPanelOpen && !_targetLocked, "unlocked reticle opens first workstation");
        CloseWorkstation();
        Aim(farm);
        SendMenuKey(Key.E);
        Require(_selectedTarget == farm && FarmPanelOpen && !_targetLocked, "new aim replaces unlocked reticle selection");
        CloseFarm();
        Aim(bench);
        _selectedTarget = bench;
        _targetLocked = true;
        camera.LookAt(WorldTargets().Single(v => v.Target == farm).Center);
        SendMenuKey(Key.E);
        Require(_selectedTarget == bench && WorkstationPanelOpen, "explicit target lock survives reticle movement");
        CloseWorkstation();

        StartSession(PlayerCharacterProfile.Create("Lab review"), 42, "Live lab.");
        SetProcess(false);
        _clock.SetPaused(false);
        _playerCharacter.SetPhysicsProcess(false);
        var actor = _world.Entities.First(e => e.Id != _world.PlayerEntityId && e.SpeciesId == "human");
        Require(_world.TrySetEntityPosition(actor.Id, new(-6, 0, 6)), "lab route fixture moves through World");
        SyncEntityViews();
        await PhysicsFrames(3);
        ToggleNpcAnimationTestPanel();
        Require(!_clock.IsPaused && !_playerCharacter.GameplayInputEnabled, "lab blocks player input without pausing simulation");
        foreach (var movement in new[] { NpcAnimationTestMovement.Walk, NpcAnimationTestMovement.Run })
        {
            var before = actor.Position;
            HandleNpcAnimationTestMovement(actor.Id, movement);
            for (var i = 0; i < 100 && IsNpcAnimationTestMoving(actor.Id); i++)
            {
                await PhysicsFrames(1);
                _clock.Advance(SimulationClock.TickIntervalSeconds, AdvanceSimulationTick);
            }
            Require(System.Numerics.Vector3.Distance(before, actor.Position) > 3.5f && !IsNpcAnimationTestMoving(actor.Id),
                movement + " finishes through live simulation while lab stays open");
        }
        _controlPause.SetManual(true);
        _clock.SetPaused(true);
        _npcAnimationTestPanel!.Close();
        Require(_clock.IsPaused, "closing lab retains manual pause");
    }
}
