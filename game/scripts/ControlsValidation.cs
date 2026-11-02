using System.Text.Json;
using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private async void RunControlsValidation()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "unnamed-controls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            _clock.SetPaused(true);
            _playerCharacter.SetPhysicsProcess(false);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(UnifiedControlSettings.Validate(UnifiedControlSettings.Defaults(), new(), out var defaultsError), "canonical binding set is conflict-free: " + defaultsError);
            Require(ControlCatalog.All.All(d => Simulation.ControllerActions.All.Contains(d.Id)), "catalog uses stable canonical identifiers");
            Require(ControlCatalog.All.Select(d => d.Id).Distinct().Count() == ControlCatalog.All.Count, "unique catalog IDs");

            var path = System.IO.Path.Combine(folder, "controls.json");
            var settings = new UnifiedControlSettings(path);
            var custom = new ControlPreferences(ToggleCrouch: true, ToggleWheels: true, StickDeadzone: 0.25f, InvertLookY: true);
            Require(settings.SetPreferences(custom, out _), "preferences save");
            settings.ApplyInputMap();
            Require(Math.Abs(InputMap.ActionGetDeadzone(ControllerActions.MoveForward) - custom.StickDeadzone) < 0.001f,
                "saved deadzone updates existing movement actions");
            Require(settings.SetPreferences(custom with { StickDeadzone = 0.35f }, out _), "deadzone change saves");
            settings.ApplyInputMap();
            Require(Math.Abs(InputMap.ActionGetDeadzone(ControllerActions.MoveForward) - 0.35f) < 0.001f,
                "reapplying updates an action created earlier");
            Require(settings.SetPreferences(custom, out _), "restore validation preferences");
            Require(settings.SetBindings(ControllerActions.BuildSlot1,
                [ControlCatalog.K(Key.Key1), ControlCatalog.K(Key.F11)], out _), "alternate binding saves");
            Require(settings.TryBind(ControllerActions.Inspect, false,
                new InputEventKey { PhysicalKeycode = Key.H, Pressed = true, ShiftPressed = true }, false, out _), "modified keyboard binding");
            var loaded = new UnifiedControlSettings(path);
            Require(loaded.Load() && loaded.Preferences == custom, "preferences reload");
            Require(loaded.Get(ControllerActions.BuildSlot1).Count == 2, "alternate bindings reload");
            Require(loaded.Get(ControllerActions.Inspect).Any(b => b.Modifier == "shift"), "modifier reload");
            Require(!loaded.SetBindings(ControllerActions.Inspect, [ControlCatalog.K(Key.E)], out var conflict)
                && conflict.Contains("already mapped"), "same-context conflict rejected");
            Require(loaded.SetBindings(ControllerActions.BuildCatalog, [ControlCatalog.K(Key.C)], out _), "disjoint-context key sharing accepted");
            Require(!loaded.SetPreferences(custom with { StickDeadzone = float.NaN }, out _), "nonfinite preferences rejected");
            Require(!loaded.SetBindings(ControllerActions.BuildCatalog,
                [new ControlBinding("axis", 999, 1)], out _), "invalid axis rejected");
            Require(!loaded.SetBindings(ControllerActions.Inspect,
                [ControlCatalog.K(Key.H) with { Contexts = ControlContext.All }], out _), "binding cannot widen action ownership");
            Require(UnifiedControlSettings.Capture(new InputEventJoypadMotion { Axis = JoyAxis.RightX, AxisValue = -0.8f })
                is { Device: "axis", Sign: -1 }, "axis capture");
            Require(UnifiedControlSettings.Capture(new InputEventJoypadMotion { Axis = JoyAxis.RightX, AxisValue = 0.1f }) is null, "axis noise ignored");
            Input.ParseInputEvent(new InputEventJoypadMotion { Device = 7, Axis = JoyAxis.TriggerLeft, AxisValue = 0.9f });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(UnifiedControlSettings.Capture(new InputEventJoypadButton { Device = 7,
                ButtonIndex = JoyButton.DpadLeft, Pressed = true }) is { } triggerChord && triggerChord.Modifier == ControlCatalog.LeftTrigger,
                "left-trigger chord is captured from actual input state");
            Input.ParseInputEvent(new InputEventJoypadMotion { Device = 7, Axis = JoyAxis.TriggerLeft, AxisValue = 0 });

            var failed = new UnifiedControlSettings(path, new SaveSlotStore(new RejectControlWrites()));
            var beforeFile = File.ReadAllText(path);
            Require(!failed.SetPreferences(custom, out _) && !failed.Preferences.ToggleCrouch
                && File.ReadAllText(path) == beforeFile, "failed write preserves disk and active preferences");

            for (var version = 1; version <= 4; version++)
            {
                var legacyPath = System.IO.Path.Combine(folder, $"legacy-{version}.json");
                var actions = PlayerButtonBindings.Actions.Where(a =>
                    (version >= 4 || a.Action != ControllerActions.BuildMode)
                    && (version >= 2 || a.Action != ControllerActions.CycleTarget)
                    && (version >= 3 || a.Action != ControllerActions.Greet))
                    .ToDictionary(a => a.Action, a => _buttonBindings.Get(a.Action));
                var raw = JsonSerializer.Serialize(new PlayerButtonBindings.SettingsFile(version, actions));
                File.WriteAllText(legacyPath, raw);
                var migration = new UnifiedControlSettings(legacyPath);
                Require(migration.Load() && migration.BackupPath is not null
                    && File.ReadAllText(migration.BackupPath) == raw, "legacy backup precedes migration " + version);
                using var saved = JsonDocument.Parse(File.ReadAllText(legacyPath));
                Require(saved.RootElement.GetProperty("Version").GetInt32() == 5, "migration writes v5 " + version);
                var again = new UnifiedControlSettings(legacyPath);
                Require(again.Load() && again.BackupPath is null, "migration occurs only once " + version);
            }

            var corruptPath = System.IO.Path.Combine(folder, "corrupt.json");
            foreach (var invalid in new[] { "{\"Version\":99}", "{\"Version\":4,\"Actions\":{}}",
                "{\"Version\":5,\"Bindings\":{},\"Preferences\":{}}" })
            {
                File.WriteAllText(corruptPath, invalid);
                var rejected = new UnifiedControlSettings(corruptPath);
                Require(!rejected.Load() && rejected.Status.Contains("preserved") && File.ReadAllText(corruptPath) == invalid,
                    "structurally invalid profile is rejected without throwing or changing the file");
            }
            File.WriteAllText(corruptPath, "{bad");
            Require(!new UnifiedControlSettings(corruptPath).Load() && File.ReadAllText(corruptPath) == "{bad", "corrupt file preserved");
            var brokenLegacy = JsonSerializer.Serialize(new { Version = 4,
                Actions = UnifiedControlSettings.LegacyActionIds.ToDictionary(id => id, _ => new { }) });
            File.WriteAllText(corruptPath, brokenLegacy);
            Require(!new UnifiedControlSettings(corruptPath).Load() && File.ReadAllText(corruptPath) == brokenLegacy,
                "invalid legacy values do not overwrite the original profile");
            var gesture = new HoldTapGesture(0.3);
            gesture.Press(0);
            Require(gesture.Advance(0.3) == HoldTapSignal.HoldStarted && gesture.IsActive, "hold enters crouch");
            Require(gesture.Release(0.4) == HoldTapSignal.HoldEnded && !gesture.IsActive, "release never dodges");
            await ValidateProductionControlRouting();
            GD.Print("CONTROLS VALIDATION PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError("CONTROLS VALIDATION FAILED: " + error);
            GetTree().Quit(1);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(folder)) File.Delete(file);
            Directory.Delete(folder);
        }
    }

    private async Task ValidateProductionControlRouting()
    {
        StartSession(PlayerCharacterProfile.Create("Controls integration"), 42, "Controls integration.");
        SetProcess(false);
        _clock.SetPaused(false);
        SynchronizeUnifiedContext();
        var intents = new List<ControlIntent>();
        void Observe(ControlIntent intent) => intents.Add(intent);
        _inputRouter.Intent += Observe;
        try
        {
            GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.W, Pressed = true });
            var before = _playerCharacter.GlobalPosition;
            GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.Q, Pressed = true });
            Require(UnifiedWheelOpen && !_clock.IsPaused && _playerCharacter.GameplayInputEnabled
                && _inputRouter.Movement.Y < -0.9f, "command wheel retains movement and running time");
            await PhysicsFrames(8);
            Require(_playerCharacter.GlobalPosition.DistanceTo(before) > 0.05f, "production player moves with a wheel open");
            GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.W, Pressed = false });
            GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.Q, Pressed = false });
            Require(!UnifiedWheelOpen, "center release closes without an order");
            Require(_controlSettings.SetBindings(ControllerActions.MoveLeft,
                [ControlCatalog.K(Key.A) with { Contexts = ControlContext.Build }], out _), "narrow movement binding context saves");
            Input.ActionPress(ControllerActions.MoveLeft);
            Require(_inputRouter.Movement.X == 0, "continuous input respects binding-specific context ownership");
            Input.ActionRelease(ControllerActions.MoveLeft);
            Require(_controlSettings.SetBindings(ControllerActions.MoveLeft,
                UnifiedControlSettings.Defaults()[ControllerActions.MoveLeft], out _), "movement binding restored");

            SendMenuKey(Key.I);
            Require(_inventoryPanel.Visible && _clock.IsPaused && !_playerCharacter.GameplayInputEnabled, "inventory owns pause");
            var rotation = _playerCharacter.Rotation;
            GetViewport().PushInput(new InputEventMouseMotion { Relative = new Vector2(50, 20) });
            Require(_playerCharacter.Rotation == rotation, "modal UI blocks pointer look at production boundary");
            var attacks = intents.Count(i => i.Action == ControllerActions.PrimaryAction);
            GetViewport().PushInput(new InputEventJoypadMotion { Device = 7, Axis = JoyAxis.TriggerRight, AxisValue = 0.9f });
            SendMenuKey(Key.I);
            Require(!_inventoryPanel.Visible && !_clock.IsPaused, "closing inventory releases only its own pause");
            GetViewport().PushInput(new InputEventJoypadMotion { Device = 7, Axis = JoyAxis.TriggerRight, AxisValue = 0.95f });
            Require(intents.Count(i => i.Action == ControllerActions.PrimaryAction) == attacks, "held trigger cannot attack after UI closes");
            GetViewport().PushInput(new InputEventJoypadMotion { Device = 7, Axis = JoyAxis.TriggerRight, AxisValue = 0 });
            GetViewport().PushInput(new InputEventJoypadMotion { Device = 7, Axis = JoyAxis.TriggerRight, AxisValue = 0.9f });
            GetViewport().PushInput(new InputEventJoypadMotion { Device = 7, Axis = JoyAxis.TriggerRight, AxisValue = 0.8f });
            Require(intents.Count(i => i.Action == ControllerActions.PrimaryAction) == attacks + 1, "axis crossing dispatches one primary intent");
            GetViewport().PushInput(new InputEventJoypadMotion { Device = 7, Axis = JoyAxis.TriggerRight, AxisValue = 0 });
            GetViewport().PushInput(new InputEventJoypadMotion { Device = 7, Axis = JoyAxis.TriggerRight, AxisValue = 0.9f });
            Require(intents.Count(i => i.Action == ControllerActions.PrimaryAction) == attacks + 2, "trigger rearms after neutral");
            GetViewport().PushInput(new InputEventJoypadMotion { Device = 7, Axis = JoyAxis.TriggerRight, AxisValue = 0 });

            GetViewport().PushInput(new InputEventJoypadButton { Device = 7, ButtonIndex = JoyButton.LeftShoulder, Pressed = true });
            GetViewport().PushInput(new InputEventJoypadButton { Device = 7, ButtonIndex = JoyButton.Y, Pressed = true });
            Require(PlacementPanelOpen, "controller utility combination opens build");
            GetViewport().PushInput(new InputEventJoypadButton { Device = 7, ButtonIndex = JoyButton.Y, Pressed = false });
            GetViewport().PushInput(new InputEventJoypadButton { Device = 7, ButtonIndex = JoyButton.LeftShoulder, Pressed = false });
            SendMenuKey(Key.Key3);
            Require(_placingWorkbench && !UnifiedWheelOpen, "number key selects build preview only");
            GetViewport().PushInput(new InputEventJoypadButton { Device = 7, ButtonIndex = JoyButton.B, Pressed = true });
            GetViewport().PushInput(new InputEventJoypadButton { Device = 7, ButtonIndex = JoyButton.B, Pressed = false });
            Require(PlacementPanelOpen && !_placingWorkbench, "first controller Back cancels preview");
            GetViewport().PushInput(new InputEventJoypadButton { Device = 7, ButtonIndex = JoyButton.B, Pressed = true });
            GetViewport().PushInput(new InputEventJoypadButton { Device = 7, ButtonIndex = JoyButton.B, Pressed = false });
            Require(!PlacementPanelOpen, "second controller Back exits build");
            var selected = _selectedBuildSlot;
            SendMenuKey(Key.Key3);
            Require(!PlacementPanelOpen && _selectedBuildSlot == selected, "number row cannot affect build outside its context");

            _controlPause.SetManual(true);
            _clock.SetPaused(true);
            SendMenuKey(Key.I);
            SendMenuKey(Key.I);
            Require(_clock.IsPaused, "menu close preserves manual pause");
            _controlPause.SetManual(false);
            _clock.SetPaused(false);
            SynchronizeUnifiedContext();
            GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.E, Pressed = true });
            _inputRouter.Advance(0.31);
            Require(UnifiedWheelOpen, "interaction hold opens latched target menu");
            GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.E, Pressed = false });
            Require(UnifiedWheelOpen, "releasing interaction opener leaves contextual choices available");
            CloseUnifiedWheel(false);

            var preferences = _controlSettings.Preferences;
            Require(_controlSettings.SetPreferences(preferences with { ToggleWheels = true }, out _), "toggle wheel preference saves");
            SendMenuKey(Key.Q);
            Require(UnifiedWheelOpen, "toggle wheel does not require a sustained press");
            SendMenuKey(Key.Escape);
            Require(!UnifiedWheelOpen, "toggle wheel cancels cleanly");
            _controlSettings.SetPreferences(preferences, out _);

            void Pad(JoyButton button)
            {
                GetViewport().PushInput(new InputEventJoypadButton { Device = 7, ButtonIndex = button, Pressed = true });
                GetViewport().PushInput(new InputEventJoypadButton { Device = 7, ButtonIndex = button, Pressed = false });
            }
            Pad(JoyButton.Back);
            Require(_inventoryPanel.Visible, "controller View opens inventory");
            _inventoryTabs.CurrentTab = 1;
            var actor = _world.Entities.Single(e => e.Id == _world.PlayerEntityId);
            var eye = _equipmentVisibilityButtons[EquipmentSlotIds.Torso];
            eye.GrabFocus();
            var visible = actor.IsEquipmentVisible(EquipmentSlotIds.Torso);
            Pad(JoyButton.Y);
            Require(actor.IsEquipmentVisible(EquipmentSlotIds.Torso) != visible, "controller Y toggles focused equipment visual");
            Pad(JoyButton.X);
            Require(_equipmentActionMenu.Visible, "controller X exposes focused equipment actions");
            _equipmentActionMenu.EmitSignal(PopupMenu.SignalName.IdPressed, 2L);
            Require(! _equipmentActionMenu.Visible && actor.IsEquipmentVisible(EquipmentSlotIds.Torso) == visible,
                "equipment action returns visibility without changing the slot");
            Pad(JoyButton.RightShoulder);
            Require(_inventoryTabs.CurrentTab == 2, "controller RB advances menu tabs");
            var category = _itemCategory.Selected;
            GetViewport().PushInput(new InputEventJoypadMotion { Device = 7, Axis = JoyAxis.TriggerRight, AxisValue = 0.9f });
            GetViewport().PushInput(new InputEventJoypadMotion { Device = 7, Axis = JoyAxis.TriggerRight, AxisValue = 0 });
            Require(_itemCategory.Selected == (category + 1) % _itemCategory.ItemCount, "controller trigger changes item category");
            Pad(JoyButton.Start);
            _settingsTabs.CurrentTab = 1;
            _mappingButtons[(ControllerActions.Inspect, false)].EmitSignal(BaseButton.SignalName.Pressed);
            GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.Shift, Pressed = true });
            Require(_captureBinding is not null, "modifier-only press waits for a chord or release");
            GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.H, ShiftPressed = true, Pressed = true });
            Require(_captureBinding is null && _controlSettings.Get(ControllerActions.Inspect).Any(b => b.Modifier == "shift"),
                "UI captures a modifier chord without binding the modifier prematurely");
            GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.H, Pressed = false });
            GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.Shift, Pressed = false });
            _mappingButtons[(ControllerActions.BuildRotateLeft, true)].EmitSignal(BaseButton.SignalName.Pressed);
            Input.ParseInputEvent(new InputEventJoypadMotion { Device = 7, Axis = JoyAxis.TriggerLeft, AxisValue = 0.9f });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(_captureBinding is not null, "trigger modifier waits for a chord or release");
            Input.ParseInputEvent(new InputEventJoypadButton { Device = 7, ButtonIndex = JoyButton.DpadLeft, Pressed = true });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(_captureBinding is null && _controlSettings.Get(ControllerActions.BuildRotateLeft)
                .Any(b => b.Modifier == ControlCatalog.LeftTrigger), "UI captures LT plus D-pad without selecting the trigger alone");
            Input.ParseInputEvent(new InputEventJoypadButton { Device = 7, ButtonIndex = JoyButton.DpadLeft, Pressed = false });
            Input.ParseInputEvent(new InputEventJoypadMotion { Device = 7, Axis = JoyAxis.TriggerLeft, AxisValue = 0 });
            ResetUnifiedControls(null);
            ClosePlayerMenus();
            GetWindow().EmitSignal(Window.SignalName.FocusExited);
            Require(!UnifiedWheelOpen && _inputRouter.Movement == Vector2.Zero, "focus loss cancels held input");
            _inputRouter.Disconnect(7);

            if (DisplayServer.GetName() != "headless")
            {
                ToggleMenu(PlayerMenu.Settings);
                _settingsTabs.CurrentTab = 1;
                Input.MouseMode = Input.MouseModeEnum.Visible;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var capture = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "unnamed-controls-settings.png");
                Require(GetViewport().GetTexture().GetImage().SavePng(capture) == Error.Ok, "controls screenshot saved");
                ClosePlayerMenus();
                OpenCommandWheel();
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), "unnamed-controls-wheel.png")) == Error.Ok, "wheel screenshot saved");
                CloseUnifiedWheel(false);
            }
        }
        finally
        {
            _inputRouter.Intent -= Observe;
            _playerCharacter.SetPhysicsProcess(false);
            _inputRouter.Cancel();
        }
    }

    private sealed class RejectControlWrites : ISaveFileSystem
    {
        public bool Exists(string path) => File.Exists(path);
        public string ReadAllText(string path) => File.ReadAllText(path);
        public void WriteAllText(string path, string contents) => throw new IOException("Injected controls write failure.");
        public void Replace(string sourcePath, string destinationPath) => throw new IOException("Injected replacement failure.");
        public void DeleteIfExists(string path) { }
    }
}
