using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private async void RunMenuValidation()
    {
        try
        {
            _sessionActive = false;
            _characterCreation.Visible = true;
            ApplyMenuState();
            Require(!_menuHud.Visible && !_inventoryPanel.Visible && !_settingsPanel.Visible && !_controls.Visible
                && !_playerCharacter.GameplayInputEnabled, "creation is not obscured by gameplay menus");
            StartSession(PlayerCharacterProfile.Create("Menu validation"), 42, "Menu validation session.");
            _clock.SetPaused(true);
            _playerCharacter.SetPhysicsProcess(false);
            _saveSlotPath = ProjectSettings.GlobalizePath("user://menu-validation-save.json");
            _buttonBindings.Path = ProjectSettings.GlobalizePath("user://menu-validation-settings.json");
            Require(_menuState.ActiveMenu is null && !_controls.Visible && !_inventoryPanel.Visible && !_settingsPanel.Visible, "session starts closed");
            SendMenuKey(Key.I);
            Require(_menuState.ActiveMenu == PlayerMenu.Inventory && _inventoryPanel.Visible && !_controls.Visible, "I opens inventory");
            Require(_carriedItems.Text.Contains("Your inventory is empty") && _characterIdentity.Text.Contains("Menu validation"), "empty inventory and identity");
            SendMenuKey(Key.I);
            Require(_menuState.ActiveMenu is null && !_inventoryPanel.Visible, "same key closes inventory");
            SendMenuKey(Key.Tab);
            Require(_controls.Visible && !_inventoryPanel.Visible && _menuState.ActiveMenu == PlayerMenu.Settlement, "Tab opens settlement");
            SendMenuKey(Key.F10);
            Require(!_controls.Visible && _settingsPanel.Visible && _menuState.ActiveMenu == PlayerMenu.Settings, "F10 switches to separate settings");
            SendMenuKey(Key.Escape);
            Require(!_settingsPanel.Visible && _menuState.ActiveMenu is null && _clock.IsPaused, "Escape closes without unpausing");
            SendMenuKey(Key.Escape);
            Require(_settingsPanel.Visible, "Escape opens game menu from gameplay");
            PressMenuButton(_settingsPanel, "Close");

            _clock.SetPaused(false);
            _playerCharacter.SetPhysicsProcess(true);
            SendMenuKey(Key.I);
            var before = _playerCharacter.GlobalPosition;
            var rotation = _playerCharacter.Rotation;
            var strikes = 0;
            _playerCharacter.StrikeReleased += (_, _) => strikes++;
            Input.ActionPress(ControllerActions.MoveRight);
            _playerCharacter._UnhandledInput(new InputEventMouseMotion { Relative = new Vector2(100, 100) });
            _playerCharacter._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            var message = _lastActionMessage;
            Input.ActionPress(ControllerActions.Collect);
            _UnhandledInput(new InputEventKey { PhysicalKeycode = Key.E, Pressed = true });
            for (var frame = 0; frame < 30; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ActionRelease(ControllerActions.MoveRight);
            Input.ActionRelease(ControllerActions.Collect);
            Require(new Vector2(before.X, before.Z).DistanceTo(new(_playerCharacter.GlobalPosition.X, _playerCharacter.GlobalPosition.Z)) < 0.001f, "menu blocks walking");
            Require(rotation == _playerCharacter.Rotation && strikes == 0 && message == _lastActionMessage, "menu blocks aim combat and world actions");
            ClosePlayerMenus();
            Require(_playerCharacter.GameplayInputEnabled, "closing restores gameplay input");
            Input.ActionPress(ControllerActions.MoveRight);
            for (var frame = 0; frame < 12; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ActionRelease(ControllerActions.MoveRight);
            Require(Math.Abs(before.X - _playerCharacter.GlobalPosition.X) > 0.01f, "gameplay movement resumes");
            _clock.SetPaused(true);
            _playerCharacter.SetPhysicsProcess(false);

            var player = _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId);
            var source = _world.ResourcePiles.First();
            Require(ResourceTransferService.TryPickUp(source.Contents, player, ItemIds.Wood, 3) == ResourceMutationResult.Success, "atomic inventory fixture transfer");
            ToggleMenu(PlayerMenu.Inventory);
            _inventoryTabs.CurrentTab = 2;
            UpdatePlayerMenuContents();
            Require(_carriedItems.Text.Contains("Wood   × 3"), "live authoritative item display");
            _itemCategory.Select(1);
            UpdatePlayerMenuContents();
            Require(_carriedItems.Text.Contains("No items in this category"), "category empty state");
            _itemCategory.Select(0);
            UpdatePlayerMenuContents();
            ToggleMenu(PlayerMenu.Settings);
            PressMenuButton(_settingsPanel, "Save game (pauses)");
            ToggleMenu(PlayerMenu.Inventory);
            Require(ResourceTransferService.TryDeliver(player, source.Contents, ItemIds.Wood, 1) == ResourceMutationResult.Success, "atomic delivery fixture");
            UpdatePlayerMenuContents();
            Require(_carriedItems.Text.Contains("Wood   × 2"), "item changes refresh");
            ToggleMenu(PlayerMenu.Settings);
            PressMenuButton(_settingsPanel, "Load game (pauses)");
            ToggleMenu(PlayerMenu.Inventory);
            Require(_carriedItems.Text.Contains("Wood   × 3") && _inventoryPanel.Visible, "load reprojects current player inventory");
            File.WriteAllText(_saveSlotPath, "invalid");
            var preserved = WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime);
            ToggleMenu(PlayerMenu.Settings);
            PressMenuButton(_settingsPanel, "Load game (pauses)");
            ToggleMenu(PlayerMenu.Inventory);
            Require(_saveLoadStatus.StartsWith("Load failed") && _carriedItems.Text.Contains("Wood   × 3")
                && preserved == WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime), "rejected load preserves live and displayed inventory");

            ToggleMenu(PlayerMenu.Settings);
            _settingsTabs.CurrentTab = 1;
            _mappingButtons[(ControllerActions.Inventory, false)].EmitSignal(BaseButton.SignalName.Pressed);
            SendMenuKey(Key.W);
            Require(_captureBinding is not null && _bindingStatus.Text.Contains("already mapped"), "mapping conflict rejected");
            SendMenuKey(Key.Escape);
            Require(_captureBinding is null && _settingsPanel.Visible, "Escape cancels capture only");
            _mappingButtons[(ControllerActions.Inventory, false)].EmitSignal(BaseButton.SignalName.Pressed);
            SendMenuKey(Key.O);
            Require(_captureBinding is null && InputMap.ActionHasEvent(ControllerActions.Inventory, new InputEventKey { PhysicalKeycode = Key.O }), "real InputMap rebound");
            var reloadedBindings = new PlayerButtonBindings(_buttonBindings.Path);
            reloadedBindings.Load();
            Require(reloadedBindings.Get(ControllerActions.Inventory).Desktop.Code == (long)Key.O, "new settings instance reload persists");
            SendMenuKey(Key.O);
            Require(_inventoryPanel.Visible && !_settingsPanel.Visible, "rebound menu key works");
            _buttonBindings.TryBind(ControllerActions.Interact, false, new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true }, out var mouseReason);
            Require(InputMap.ActionHasEvent(ControllerActions.Interact, new InputEventMouseButton { ButtonIndex = MouseButton.Middle }), mouseReason);
            _buttonBindings.TryBind(ControllerActions.Settings, true, new InputEventJoypadButton { ButtonIndex = JoyButton.LeftShoulder, Pressed = true }, out var padReason);
            Require(InputMap.ActionHasEvent(ControllerActions.Settings, new InputEventJoypadButton { ButtonIndex = JoyButton.LeftShoulder }), padReason);
            var validPath = _buttonBindings.Path;
            _buttonBindings.Path = validPath + "/invalid-child.json";
            Require(!_buttonBindings.TryBind(ControllerActions.Inventory, false, new InputEventKey { PhysicalKeycode = Key.P, Pressed = true }, out _)
                && _buttonBindings.Get(ControllerActions.Inventory).Desktop.Code == (long)Key.O, "failed settings write retains active binding");
            _buttonBindings.Path = validPath;
            ToggleMenu(PlayerMenu.Settings);
            PressMenuButton(_settingsPanel, "Restore default button mapping");
            Require(_bindingStatus.Text == "Button mapping saved.", "restore default mapping persists");
            RefreshMappingButtons();
            Require(InputMap.ActionHasEvent(ControllerActions.Inventory, new InputEventKey { PhysicalKeycode = Key.I }), "defaults restored in InputMap");

            Require(_buttonBindings.TryBind(ControllerActions.AttackLeft, false, new InputEventKey { PhysicalKeycode = Key.K, Pressed = true }, out _), "left attack rebind");
            Require(_buttonBindings.TryBind(ControllerActions.AttackRight, false, new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true }, out _), "right attack mouse rebind");
            _clock.SetPaused(false);
            _playerCharacter.SetPhysicsProcess(true);
            ClosePlayerMenus();
            strikes = 0;
            SendMenuMouse(MouseButton.Left);
            for (var frame = 0; frame < 30; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Require(strikes == 0, "old hardcoded mouse attack is removed");
            SendMenuKey(Key.K);
            for (var frame = 0; frame < 30; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Require(strikes == 1, "rebound keyboard attack reaches actual swing");
            SendMenuMouse(MouseButton.Middle);
            for (var frame = 0; frame < 30; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Require(strikes == 2, "rebound mouse attack reaches actual swing");
            _clock.SetPaused(true);
            _playerCharacter.SetPhysicsProcess(false);
            Require(_buttonBindings.RestoreDefaults(out _), "defaults restored after attack check");
            RefreshMappingButtons();
            ToggleMenu(PlayerMenu.Settings);

            if (DisplayServer.GetName() != "headless")
            {
                ToggleMenu(PlayerMenu.Inventory);
                var directory = ProjectSettings.GlobalizePath("res://../docs/evidence/player-menus");
                Directory.CreateDirectory(directory);
                foreach (var (tab, name) in new[] { (0, "status"), (1, "equipment"), (2, "items"), (3, "other") })
                {
                    _inventoryTabs.CurrentTab = tab;
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    Require(GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png")) == Error.Ok, name + " screenshot");
                }
                ToggleMenu(PlayerMenu.Settings);
                _settingsTabs.CurrentTab = 1;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, "settings.png")) == Error.Ok, "settings screenshot");
                _settingsTabs.CurrentTab = 0;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, "settings-game.png")) == Error.Ok, "save screen screenshot");
                ToggleMenu(PlayerMenu.Settlement);
                _settlementTabs.CurrentTab = 1;
                SelectEntity(_world.Entities.First(entity => entity.Id != _world.PlayerEntityId && entity.SpeciesId == "human").Id);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, "settlement.png")) == Error.Ok, "settlement screenshot");
                GetWindow().Size = new Vector2I(960, 600);
                ToggleMenu(PlayerMenu.Inventory);
                _inventoryTabs.CurrentTab = 0;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(_inventoryPanel.GetGlobalRect().End.X <= GetViewport().GetVisibleRect().End.X, "narrow window panel stays inside viewport");
                Require(GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, "status-960.png")) == Error.Ok, "narrow status screenshot");
            }
            ResetScenario();
            Require(_menuState.ActiveMenu is null && !_inventoryPanel.Visible && _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId).Inventory.Count == 0, "reset clears stale character display");
            GD.Print("MENU VALIDATION PASSED: distinct screens, tabs, live inventory, input isolation, persistence recovery and button mapping.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"MENU VALIDATION FAILED: {exception}");
            GetTree().Quit(1);
        }
    }

    private static void PressMenuButton(Control panel, string text) => panel.FindChildren("*", "Button", true, false)
        .OfType<Button>().Single(button => button.Text == text).EmitSignal(BaseButton.SignalName.Pressed);

    private void SendMenuKey(Key key)
    {
        GetViewport().PushInput(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        GetViewport().PushInput(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
    }

    private void SendMenuMouse(MouseButton button)
    {
        var position = new Vector2(950, 650);
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = button, Position = position, Pressed = true });
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = button, Position = position, Pressed = false });
    }
}
