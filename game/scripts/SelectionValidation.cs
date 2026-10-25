using System.Text.Json;
using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private async void RunSelectionValidation()
    {
        try
        {
            StartSession(PlayerCharacterProfile.Create("Selection validation"), 42, "Selection validation session.");
            // Freeze fixture movement without using pause, so input follows the real gameplay route.
            SetProcess(false);
            _playerCharacter.SetPhysicsProcess(false);
            var camera = GetNode<Camera3D>("Camera3D");
            camera.Current = true;
            camera.GlobalPosition = new Vector3(-3, 10, 22);
            camera.LookAt(new Vector3(0, 0, -8));
            var human = _world.Entities.First(entity => entity.Id != _world.PlayerEntityId && entity.SpeciesId == "human");
            var creature = _world.Entities.First(entity => entity.SpeciesId == "wolf");
            foreach (var (id, view) in _entityViews) view.Visible = id == human.Id || id == creature.Id;
            _world.TrySetEntityPosition(human.Id, new(-3, 0, -8));
            _world.TrySetEntityPosition(creature.Id, new(3, 0, -10));
            SyncEntityViews();
            foreach (var source in _world.FoodSources.ToArray()) _world.TryRemoveFoodSource(source.Id);
            foreach (var source in _world.ResourcePiles.ToArray()) _world.TryRemoveResourcePile(source.Id);
            foreach (var source in _world.Stockpiles.ToArray()) _world.TryRemoveStockpile(source.Id);
            var food = new ResourceContainer("Selection food", 20); food.TryAdd(ItemIds.Food, 7);
            var wood = new ResourceContainer("Selection wood", 20); wood.TryAdd(ItemIds.Wood, 9);
            var stock = new ResourceContainer("Selection stock", 20); stock.TryAdd(ItemIds.Wood, 2);
            _world.AddFoodSource("selection-food", new(0, 0, -12), food);
            _world.AddResourcePile("selection-wood", new(-5, 0, -12), wood);
            _world.AddStockpile("selection-stock", new(5, 0, -12), stock);
            ClearResourceMarkers(); RebuildResourceMarkersFromWorld();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var expectedTargets = new HashSet<WorldTarget>
            {
                new(TargetKind.Entity, human.Id),
                new(TargetKind.Entity, creature.Id),
                new(TargetKind.Food, "selection-food"),
                new(TargetKind.ResourcePile, "selection-wood"),
                new(TargetKind.Stockpile, "selection-stock"),
                new(TargetKind.Farm, _world.Farms.Single().Id)
            };
            var visibleTargets = VisibleWorldTargets().Select(view => view.Target).ToHashSet();
            var missingTargets = expectedTargets.Except(visibleTargets).ToArray();
            Require(missingTargets.Length == 0,
                $"all expected entities, resources and farm are visible; missing: {string.Join(", ", missingTargets.Select(target => $"{target.Kind}:{target.Id}"))}; visible: {string.Join(", ", visibleTargets.Select(target => $"{target.Kind}:{target.Id}"))}");
            var snapshot = WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime);
            var strikes = 0; _playerCharacter.StrikeReleased += (_, _) => strikes++;
            var seen = new HashSet<WorldTarget>();
            WorldTarget? first = null;
            for (var index = 0; index < visibleTargets.Count; index++)
            {
                SendMenuKey(Key.T);
                Require(_selectedTarget is not null && seen.Add(_selectedTarget), "cycle selects each visible target once");
                first ??= _selectedTarget;
                Require(_menuState.ActiveMenu is null && !_controls.Visible && _targetHud.Visible && _targetRing.Visible, "selection is visible without a menu");
                if (_selectedTarget!.Kind == TargetKind.Entity)
                    Require(_selectedEntityId == _selectedTarget.Id && _inspectorLabel.Text.Contains(_selectedTarget.Id), "existing inspector and actions share entity selection");
            }
            SendMenuKey(Key.T);
            Require(_selectedTarget == first, "cycle wraps to first target");
            Require(snapshot == WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime) && strikes == 0 && !_playerCharacter.IsAttacking, "selection is read-only and does not attack");
            var before = _selectedTarget;
            GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.T, Keycode = Key.T, Pressed = true, Echo = true });
            Require(_selectedTarget == before, "held-key echo does not cycle repeatedly");
            foreach (var menu in Enum.GetValues<PlayerMenu>())
            {
                ToggleMenu(menu); SendMenuKey(Key.T);
                Require(_selectedTarget == before && !_targetHud.Visible && !_targetRing.Visible, "menus suppress world selection and feedback");
                ClosePlayerMenus();
            }
            _sessionActive = false; SendMenuKey(Key.T);
            Require(_selectedTarget == before, "character creation suppresses selection");
            _sessionActive = true;
            _clock.SetPaused(true); SendMenuKey(Key.T);
            Require(_selectedTarget == before, "paused gameplay suppresses selection");
            _clock.SetPaused(false);

            var humanTarget = new WorldTarget(TargetKind.Entity, human.Id);
            var humanView = WorldTargets().Single(view => view.Target == humanTarget);
            var blocker = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2, 2, 2) },
                Position = camera.GlobalPosition.Lerp(humanView.Center, 0.5f) };
            _scenery!.AddChild(blocker);
            Require(!VisibleWorldTargets().Any(view => view.Target == humanTarget), "visible scenery blocks target line of sight");
            blocker.Visible = false;
            Require(VisibleWorldTargets().Any(view => view.Target == humanTarget), "hidden scenery does not block targeting");
            _scenery.RemoveChild(blocker); blocker.QueueFree();
            _world.TrySetEntityPosition(human.Id, new(0, 0, 60)); SyncEntityViews();
            Require(!VisibleWorldTargets().Any(view => view.Target == humanTarget), "out-of-range and behind-camera target excluded");
            _world.TrySetEntityPosition(human.Id, new(-3, 0, -8)); SyncEntityViews();
            SelectEntity(human.Id); UpdateWorldSelection();
            Require(_selectedTarget == humanTarget && _targetRing.Visible, "Settlement selection shares gameplay highlight");
            RemoveSelectedEntity(); UpdateWorldSelection();
            Require(_selectedTarget is null && !_targetRing.Visible, "entity removal clears feedback");
            _selectedTarget = new(TargetKind.Food, "selection-food"); UpdateWorldSelection();
            Require(_targetHud.Text.Contains("food: 7"), "food count from authoritative container");
            food.TryRemove(ItemIds.Food, 1); UpdateWorldSelection();
            Require(_targetHud.Text.Contains("food: 6"), "live resource counts refresh");

            if (DisplayServer.GetName() != "headless")
            {
                var directory = ProjectSettings.GlobalizePath("res://../docs/evidence/world-selection");
                Directory.CreateDirectory(directory);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, "food-target.png")) == Error.Ok, "rendered food target evidence");
                _selectedTarget = new(TargetKind.Entity, creature.Id); UpdateWorldSelection();
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, "creature-target.png")) == Error.Ok, "rendered creature target evidence");
            }
            _selectedTarget = new(TargetKind.Food, "selection-food");
            _world.TryRemoveFoodSource("selection-food"); UpdateWorldSelection();
            Require(_selectedTarget is null && !_targetRing.Visible, "removed resource clears selection");
            foreach (var view in _entityViews.Values) view.Visible = false;
            _farmView.Visible = false;
            _workbench.Visible = false;
            _world.TryRemoveResourcePile("selection-wood"); _world.TryRemoveStockpile("selection-stock");
            SendMenuKey(Key.T);
            Require(_selectedTarget is null && _lastActionMessage.Contains("No visible targets"), "no-target case clears safely");

            _buttonBindings.Path = ProjectSettings.GlobalizePath("user://selection-validation-settings.json");
            var legacy = PlayerButtonBindings.Actions.Where(entry => entry.Action != ControllerActions.CycleTarget
                    && entry.Action != ControllerActions.Greet && entry.Action != ControllerActions.BuildMode)
                .ToDictionary(entry => entry.Action, entry => _buttonBindings.Get(entry.Action));
            legacy[ControllerActions.Interact] = new(new("key", (long)Key.G), null);
            legacy[ControllerActions.Inventory] = new(new("key", (long)Key.T), new("joypad", (long)JoyButton.Y));
            var legacyJson = JsonSerializer.Serialize(new PlayerButtonBindings.SettingsFile(1, legacy));
            File.WriteAllText(_buttonBindings.Path, legacyJson); _buttonBindings.Load();
            Require(_buttonBindings.Get(ControllerActions.Interact).Desktop.Code == (long)Key.G
                && _buttonBindings.Get(ControllerActions.Inventory).Desktop.Code == (long)Key.T
                && _buttonBindings.Get(ControllerActions.CycleTarget).Desktop.Code == (long)Key.F9
                && File.ReadAllText(_buttonBindings.Path) == legacyJson, "legacy preferences preserved, target key avoids conflict, migration does not overwrite file");
            Require(_buttonBindings.Get(ControllerActions.Greet).Desktop.Code == (long)Key.F11,
                "legacy greeting migration avoids existing G and F9 bindings");
            var versionTwo = PlayerButtonBindings.Actions.Where(entry => entry.Action != ControllerActions.Greet
                    && entry.Action != ControllerActions.BuildMode)
                .ToDictionary(entry => entry.Action, entry => _buttonBindings.Get(entry.Action));
            var versionTwoJson = JsonSerializer.Serialize(new PlayerButtonBindings.SettingsFile(2, versionTwo));
            File.WriteAllText(_buttonBindings.Path, versionTwoJson);
            _buttonBindings.Load();
            Require(_buttonBindings.Get(ControllerActions.Interact).Desktop.Code == (long)Key.G
                && _buttonBindings.Get(ControllerActions.Greet).Desktop.Code == (long)Key.F11
                && File.ReadAllText(_buttonBindings.Path) == versionTwoJson,
                "version two migration preserves bindings and source file");
            File.WriteAllText(_buttonBindings.Path, JsonSerializer.Serialize(new PlayerButtonBindings.SettingsFile(3, versionTwo)));
            var invalidPreferences = new PlayerButtonBindings(_buttonBindings.Path);
            invalidPreferences.Load();
            Require(invalidPreferences.Status.StartsWith("Button mapping not loaded", StringComparison.Ordinal)
                && invalidPreferences.Get(ControllerActions.Greet).Desktop.Code == (long)Key.G,
                "incomplete current settings retain defaults without throwing");
            var versionThree = PlayerButtonBindings.Actions.Where(entry => entry.Action != ControllerActions.BuildMode)
                .ToDictionary(entry => entry.Action, entry => _buttonBindings.Get(entry.Action));
            var versionThreeJson = JsonSerializer.Serialize(new PlayerButtonBindings.SettingsFile(3, versionThree));
            File.WriteAllText(_buttonBindings.Path, versionThreeJson);
            var upgradedPreferences = new PlayerButtonBindings(_buttonBindings.Path);
            upgradedPreferences.Load();
            Require(!upgradedPreferences.Status.StartsWith("Button mapping not loaded", StringComparison.Ordinal)
                && upgradedPreferences.Get(ControllerActions.BuildMode).Desktop.Code == (long)Key.B
                && upgradedPreferences.Get(ControllerActions.BuildMode).Controller?.Code == (long)JoyButton.LeftStick
                && File.ReadAllText(_buttonBindings.Path) == versionThreeJson,
                "version three preferences preserve actions while adding build-mode bindings without rewriting the legacy file");
            Require(_buttonBindings.TryBind(ControllerActions.CycleTarget, false, new InputEventKey { PhysicalKeycode = Key.K, Pressed = true }, out _), "target key rebind persists");
            var preferences = new PlayerButtonBindings(_buttonBindings.Path); preferences.Load();
            Require(preferences.Get(ControllerActions.CycleTarget).Desktop.Code == (long)Key.K, "version 2 target binding reload");
            Require(!_buttonBindings.TryBind(ControllerActions.CycleTarget, false, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true }, out _)
                && _buttonBindings.Get(ControllerActions.CycleTarget).Desktop.Code == (long)Key.K, "attack conflict rejected atomically");
            File.WriteAllText(_buttonBindings.Path, "{invalid"); _buttonBindings.Load();
            Require(_buttonBindings.Get(ControllerActions.CycleTarget).Desktop.Code == (long)Key.K, "invalid settings retain active target binding");
            _selectedTarget = new(TargetKind.Entity, creature.Id); ResetScenario();
            Require(_selectedTarget is null, "reset clears selection");
            SetProcess(false); _playerCharacter.SetPhysicsProcess(false);
            camera.Current = true; camera.GlobalPosition = new Vector3(0, 8, 6); camera.LookAt(new Vector3(0, 0, -10));
            _world.TrySetEntityPosition(creature.Id, new(3, 0, -10)); SyncEntityViews();
            SendMenuKey(Key.K);
            Require(_selectedTarget is not null && _menuState.ActiveMenu is null, "rebound key reaches dispatched world targeting");
            _saveSlotPath = ProjectSettings.GlobalizePath("user://selection-validation-save.json");
            SaveScenario(); LoadScenario();
            Require(_selectedTarget is null && !_targetRing.Visible, "load clears old target references");
            _clock.SetPaused(false); _playerCharacter.SetPhysicsProcess(true); ClosePlayerMenus();
            var rotation = _playerCharacter.Rotation;
            _playerCharacter._UnhandledInput(new InputEventMouseMotion { Relative = new Vector2(10, 0) });
            Require(_playerCharacter.Rotation != rotation, "mouse look retained");
            SendMenuMouse(MouseButton.Left);
            Require(_playerCharacter.IsAttacking, "normal mouse attack retained");
            _buttonBindings.RestoreDefaults(out _);
            var player = _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId);
            var greetingTarget = _world.Entities.First(entity => entity.SpeciesId == "human" && entity.Id != player.Id);
            _world.TrySetEntityPosition(greetingTarget.Id, player.Position + new System.Numerics.Vector3(1.5f, 0, 0));
            SyncEntityViews(); SelectEntity(greetingTarget.Id);
            SendMenuKey(Key.G);
            Require(_lastActionMessage.StartsWith("Successfully performed greet.", StringComparison.Ordinal) && _menuState.ActiveMenu is null,
                "default G greets the human gameplay target without opening a menu");
            GD.Print("SELECTION VALIDATION PASSED: menu-free cycling, visibility/occlusion, shared selection, live feedback, stale cleanup, preferences migration and combat/look regression.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"SELECTION VALIDATION FAILED: {exception}"); GetTree().Quit(1);
        }
    }
}
