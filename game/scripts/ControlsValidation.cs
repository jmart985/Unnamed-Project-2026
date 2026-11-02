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
            Require(UnifiedControlSettings.Validate(UnifiedControlSettings.Defaults(), new(), out _), "canonical binding set is conflict-free");
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
            var gesture = new HoldTapGesture(0.3);
            gesture.Press(0);
            Require(gesture.Advance(0.3) == HoldTapSignal.HoldStarted && gesture.IsActive, "hold enters crouch");
            Require(gesture.Release(0.4) == HoldTapSignal.HoldEnded && !gesture.IsActive, "release never dodges");
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

    private sealed class RejectControlWrites : ISaveFileSystem
    {
        public bool Exists(string path) => File.Exists(path);
        public string ReadAllText(string path) => File.ReadAllText(path);
        public void WriteAllText(string path, string contents) => throw new IOException("Injected controls write failure.");
        public void Replace(string sourcePath, string destinationPath) => throw new IOException("Injected replacement failure.");
        public void DeleteIfExists(string path) { }
    }
}
