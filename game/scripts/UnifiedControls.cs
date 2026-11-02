using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private UnifiedControlSettings _controlSettings = null!;
    private UnifiedInputRouter _inputRouter = null!;
    private ControlWheel _controlWheel = null!;
    private readonly ControlPauseState _controlPause = new();
    private bool _controlUiPause;
    private bool _physicsBeforeControlMenu;
    private bool _choosingSquadDestination;
    private bool _targetFlickReady = true;
    private InputEvent? _captureModifierEvent;
    private Label _utilityGuide = null!;
    private PopupMenu _equipmentActionMenu = null!;
    private string? _equipmentActionSlot;
    private Control? _buildCatalogPanel;
    private ConfirmationDialog _quickLoadDialog = null!;
    private int _suspendedBuildSlot;
    private bool UnifiedWheelOpen => _controlWheel?.IsOpen == true;

    private void InitializeUnifiedInput()
    {
        var validating = OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--validate-", StringComparison.Ordinal));
        _controlSettings = new(validating
            ? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "unnamed-validation-controls-" + Guid.NewGuid().ToString("N") + ".json")
            : ProjectSettings.GlobalizePath("user://player-settings.json"));
        if (!validating) _controlSettings.Load();
        ApplyUnifiedProfile();
        _inputRouter = new(_controlSettings);
        _inputRouter.Intent += DispatchUnifiedIntent;
        _inputRouter.PointerLook += motion => _playerCharacter.ApplyControlLook(motion, _controlSettings.Preferences);
        _inputRouter.WheelPointer += position => _controlWheel?.Pointer(position);
        _playerCharacter.Controls = _inputRouter;
        GetWindow().FocusExited += CancelUnifiedInput;
        Input.JoyConnectionChanged += (device, connected) =>
        {
            if (!connected) { _inputRouter.Disconnect((int)device); CloseUnifiedWheel(false); }
        };
    }

    private void ApplyUnifiedProfile()
    {
        _controlSettings.ApplyInputMap();
        foreach (var id in new[] { ControllerActions.Attack, ControllerActions.AttackLeft, ControllerActions.AttackRight,
            ControllerActions.Collect, ControllerActions.Interact, ControllerActions.Greet, ControllerActions.Rest,
            ControllerActions.DebugRoute, ControllerActions.DebugUnreachable, ControllerActions.DebugCancel, ControllerActions.DebugRemoveGoal })
            InputMap.ActionEraseEvents(id);
        foreach (var (native, semantic) in new[] { ("ui_accept", ControllerActions.MenuConfirm), ("ui_cancel", ControllerActions.CloseMenu) })
        {
            InputMap.ActionEraseEvents(native);
            foreach (var binding in _controlSettings.Get(semantic).Where(b => ControlInputPolicy.Accepts(ControlContext.Modal,
                b.Contexts ?? ControlCatalog.Get(semantic).Contexts))) InputMap.ActionAddEvent(native, UnifiedControlSettings.ToEvent(binding));
        }
    }

    private void ConfigureUnifiedOverlays()
    {
        _controlWheel = new ControlWheel { Name = "ContextWheel" };
        _menuHud.AddChild(_controlWheel);
        _utilityGuide = MenuLabel();
        _utilityGuide.Position = new Vector2(24, 150);
        _utilityGuide.CustomMinimumSize = new Vector2(620, 0);
        _utilityGuide.MouseFilter = Control.MouseFilterEnum.Ignore;
        _utilityGuide.Hide();
        _menuHud.AddChild(_utilityGuide);
        _equipmentActionMenu = new PopupMenu { Name = "EquipmentItemActions" };
        AddChild(_equipmentActionMenu);
        _equipmentActionMenu.IdPressed += ExecuteEquipmentMenuAction;
        _quickLoadDialog = new ConfirmationDialog { Title = "Load saved game?",
            DialogText = "Unsaved progress will be replaced by the saved game.", OkButtonText = "Load" };
        AddChild(_quickLoadDialog);
        _quickLoadDialog.Confirmed += () =>
        {
            _controlPause.SetManual(true);
            LoadScenario();
            SynchronizeUnifiedContext();
        };
        _quickLoadDialog.Canceled += SynchronizeUnifiedContext;
    }

    private void SynchronizeUnifiedContext()
    {
        if (_inputRouter is null || !_sessionActive) return;
        var pausingModal = _menuState.ActiveMenu is not null || WorkstationPanelOpen || FarmPanelOpen || ConstructionPanelOpen
            || _buildCatalogPanel?.Visible == true || _quickLoadDialog?.Visible == true
            || _equipmentActionMenu?.Visible == true;
        var modal = pausingModal || _npcAnimationTestPanel?.IsOpen == true;
        if (!_controlUiPause && _clock.IsPaused != _controlPause.IsPaused) _controlPause.SetManual(_clock.IsPaused);
        _controlPause.SetOwner("menus", pausingModal);
        if (pausingModal && !_controlUiPause)
        { _physicsBeforeControlMenu = _playerCharacter.IsPhysicsProcessing(); _playerCharacter.SetPhysicsProcess(false); }
        else if (!pausingModal && _controlUiPause)
            _playerCharacter.SetPhysicsProcess(_physicsBeforeControlMenu && !_controlPause.ManuallyPaused);
        if (pausingModal || _controlUiPause) _clock.SetPaused(_controlPause.IsPaused);
        _controlUiPause = pausingModal;
        var text = GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit;
        _inputRouter.SetContext(ControlInputPolicy.Resolve(_captureBinding is not null, modal && text, modal, UnifiedWheelOpen, PlacementPanelOpen),
            UnifiedWheelOpen ? _controlWheel.Opener : null);
        var awake = _world.PlayerEntityId is { } id && _world.TryGetEntity(id, out var player) && player is not null
            && player.SleepState != SleepState.Sleeping;
        var enabled = !modal && !_clock.IsPaused && awake;
        if (_playerCharacter.GameplayInputEnabled != enabled) _playerCharacter.SetGameplayInputEnabled(enabled);
        _playerCharacter.MouseLookEnabled = enabled && !UnifiedWheelOpen;
    }

    private void AdvanceUnifiedControls(double delta)
    {
        SynchronizeUnifiedContext();
        _inputRouter.Advance(delta);
        _utilityGuide.Visible = !UnifiedWheelOpen && _inputRouter.Context is ControlContext.Gameplay or ControlContext.Build
            && _inputRouter.Strength(ControllerActions.Utility) > 0;
        if (_utilityGuide.Visible)
            _utilityGuide.Text = string.Join("\n", new[] { ControllerActions.CommandWheel, ControllerActions.BuildMode,
                ControllerActions.Inspect, ControllerActions.SocialWheel, ControllerActions.Settlement,
                ControllerActions.ToggleView, ControllerActions.Pause }.Select(id => UnifiedLabel(id, true) + " · " + ControlCatalog.Get(id).Label));
        if (UnifiedWheelOpen && _inputRouter.ControllerLast) _controlWheel.Stick(_inputRouter.Look(true));
        if (_inputRouter.Context is ControlContext.Gameplay or ControlContext.Build && !_clock.IsPaused)
        {
            var zoom = _inputRouter.Strength(ControllerActions.ZoomOut) - _inputRouter.Strength(ControllerActions.ZoomIn);
            _playerCharacter.AdjustControlZoom(zoom * (float)delta * 4);
            var horizontal = _inputRouter.Look().X;
            if (Math.Abs(horizontal) < 0.25f) _targetFlickReady = true;
            if (_targetLocked && _targetFlickReady && Math.Abs(horizontal) > 0.80f)
            { _targetFlickReady = false; CycleWorldTarget(horizontal > 0 ? 1 : -1); }
        }
    }

    private void CancelUnifiedInput()
    {
        _captureBinding = null;
        _captureModifierEvent = null;
        _inputRouter?.Cancel();
        _playerCharacter.SetGameplayInputEnabled(false);
        CloseUnifiedWheel(false);
        _choosingSquadDestination = false;
        SynchronizeUnifiedContext();
    }

    private bool HandleUnifiedInput(InputEvent input)
    {
        if (!_sessionActive) return false;
        SynchronizeUnifiedContext();
        if (_captureBinding is { } capture)
        {
            if (input is InputEventKey { Pressed: true, PhysicalKeycode: Key.Escape })
            { _captureBinding = null; _captureModifierEvent = null; _bindingStatus.Text = "Mapping cancelled."; return true; }
            if (input is InputEventKey { Pressed: true } modifierKey && modifierKey.PhysicalKeycode is Key.Shift or Key.Ctrl or Key.Alt or Key.Meta
                || input is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.LeftShoulder }
                || input is InputEventJoypadMotion { Axis: JoyAxis.TriggerLeft, AxisValue: >= 0.65f })
            { _captureModifierEvent = (InputEvent)input.Duplicate(); return true; }
            if (input is InputEventKey { Pressed: false } releasedKey && _captureModifierEvent is InputEventKey pendingKey
                && releasedKey.PhysicalKeycode == pendingKey.PhysicalKeycode)
            { input = pendingKey; _captureModifierEvent = null; }
            else if (input is InputEventJoypadButton { Pressed: false, ButtonIndex: JoyButton.LeftShoulder }
                && _captureModifierEvent is InputEventJoypadButton pendingPad)
            { input = pendingPad; _captureModifierEvent = null; }
            else if (input is InputEventJoypadMotion { Axis: JoyAxis.TriggerLeft, AxisValue: < 0.35f }
                && _captureModifierEvent is InputEventJoypadMotion pendingTrigger)
            { input = pendingTrigger; _captureModifierEvent = null; }
            else if (input.IsPressed() || input is InputEventJoypadMotion) _captureModifierEvent = null;
            if (_controlSettings.TryBind(capture.Action, capture.Controller, input, _captureAlternate, out var reason))
            {
                _captureBinding = null;
                _inputRouter.Cancel();
                ApplyUnifiedProfile();
                _bindingStatus.Text = reason;
                RefreshMappingButtons();
            }
            else if (input.IsPressed()) _bindingStatus.Text = reason;
            SynchronizeUnifiedContext();
            return true;
        }
        return _inputRouter.Handle(input);
    }

    private void DispatchUnifiedIntent(ControlIntent intent)
    {
        if (!intent.Pressed)
        {
            if (UnifiedWheelOpen && _controlWheel.Opener == intent.Action) CloseUnifiedWheel(true);
            return;
        }
        var action = intent.Action;
        if (UnifiedWheelOpen)
        {
            if (action == ControllerActions.CloseMenu) CloseUnifiedWheel(false);
            else if (action == ControllerActions.MenuConfirm || action == _controlWheel.Opener
                && _controlSettings.Preferences.ToggleWheels) CloseUnifiedWheel(true);
            return;
        }
        if (action == ControllerActions.CloseMenu) { BackUnified(); return; }
        if (action == ControllerActions.Inventory) { ToggleMenu(PlayerMenu.Inventory); return; }
        if (action == ControllerActions.Settlement) { ToggleMenu(PlayerMenu.Settlement); return; }
        if (action == ControllerActions.Settings) { ToggleMenu(PlayerMenu.Settings); return; }
        if (action == ControllerActions.Help)
        { if (_menuState.ActiveMenu != PlayerMenu.Settings) ToggleMenu(PlayerMenu.Settings); _settingsTabs.CurrentTab = 1; return; }
        if (_inputRouter.Context == ControlContext.Modal) { HandleUnifiedMenuAction(action); return; }
        if (action == ControllerActions.Pause) { TogglePause(); return; }
        if (_clock.IsPaused) return;
        if (_world.PlayerEntityId is { } sleepingId && _world.TryGetEntity(sleepingId, out var sleepingPlayer)
            && sleepingPlayer?.SleepState == SleepState.Sleeping)
        {
            if (action == ControllerActions.SocialWheel) OpenSocialWheel();
            else ShowActionFeedback("You are sleeping. Open self/social actions to wake up.");
            return;
        }
        if (_choosingSquadDestination && action is ControllerActions.PrimaryAction or ControllerActions.SecondaryAction)
        {
            if (action == ControllerActions.PrimaryAction && TryGetLookedAtGroundPoint(out var point))
                ShowActionFeedback(_squad!.Execute(SquadOrderKind.MoveTo, new(point.X, 0, point.Z)).Reason);
            _choosingSquadDestination = false;
            return;
        }
        if (action == ControllerActions.BuildMode)
        { if (PlacementPanelOpen) CancelHutPlacement(); else BeginBuildMode(); SynchronizeUnifiedContext(); return; }
        if (action == ControllerActions.ToggleView) { _playerCharacter.ToggleControlView(); return; }
        if (action == ControllerActions.ZoomIn) { _playerCharacter.AdjustControlZoom(-1); return; }
        if (action == ControllerActions.ZoomOut) { _playerCharacter.AdjustControlZoom(1); return; }
        if (action == ControllerActions.Slower) { _clock.SetSpeed(SimulationSpeed.OneX); return; }
        if (action == ControllerActions.Faster) { _clock.SetSpeed(SimulationSpeed.FourX); return; }
        if (action == ControllerActions.QuickSave)
        {
            var paused = _clock.IsPaused;
            var processing = _playerCharacter.IsPhysicsProcessing();
            SaveScenario();
            _controlPause.SetManual(paused);
            _clock.SetPaused(paused);
            _playerCharacter.SetPhysicsProcess(processing);
            return;
        }
        if (action == ControllerActions.QuickLoad) { _quickLoadDialog.PopupCentered(); SynchronizeUnifiedContext(); return; }
        if (action == ControllerActions.Jump) { _playerCharacter.RequestControlJump(); return; }
        if (PlacementPanelOpen)
        {
            var slot = Enumerable.Range(1, 10).FirstOrDefault(i => action == "build_slot_" + i);
            if (slot > 0) SelectBuildHotbarSlot(slot);
            else if (action == ControllerActions.BuildPlace)
            {
                if (_placingHut || _placingFireplace || _placingWorkbench || _placingFarm) ConfirmPlacement();
                else ShowActionFeedback("Select a build option first.");
            }
            else if (action == ControllerActions.BuildCancel) BackUnified();
            else if (action == ControllerActions.BuildPrevious) SelectBuildHotbarSlot(_selectedBuildSlot <= 1 ? 4 : _selectedBuildSlot - 1);
            else if (action == ControllerActions.BuildNext) SelectBuildHotbarSlot(_selectedBuildSlot is < 1 or >= 4 ? 1 : _selectedBuildSlot + 1);
            else if (action == ControllerActions.BuildWheel) OpenBuildWheel();
            else if (action == ControllerActions.BuildCatalog) OpenUnifiedBuildCatalog();
            SynchronizeUnifiedContext();
            return;
        }
        if (action == ControllerActions.PrimaryAction || action == ControllerActions.SecondaryAction)
        { _playerCharacter.PerformEquipmentAction(action == ControllerActions.PrimaryAction); return; }
        if (action == ControllerActions.CycleTarget) { CycleWorldTarget(); return; }
        if (action == ControllerActions.PreviousTarget) { CycleWorldTarget(-1); return; }
        if (action == ControllerActions.LockTarget)
        { if (_targetLocked) ClearTargetLock(); else CycleWorldTarget(); return; }
        if (action == ControllerActions.Inspect)
        { FocusReticleTarget(); ToggleMenu(PlayerMenu.Settlement); _settlementTabs.CurrentTab = 1; return; }
        if (action == ControllerActions.DefaultInteraction) { FocusReticleTarget(); DefaultUnifiedInteraction(); }
        else if (action == ControllerActions.ContextInteraction) { FocusReticleTarget(); OpenInteractionWheel(); }
        else if (action == ControllerActions.SocialWheel) OpenSocialWheel();
        else if (action == ControllerActions.CommandWheel) OpenCommandWheel();
        SynchronizeUnifiedContext();
    }

    private void FocusReticleTarget()
    {
        if (_targetLocked && _selectedTarget is not null && TargetExists(_selectedTarget)) return;
        var camera = GetViewport().GetCamera3D();
        if (camera is null) return;
        var center = GetViewport().GetVisibleRect().GetCenter();
        var from = camera.ProjectRayOrigin(center);
        var to = from + camera.ProjectRayNormal(center) * TargetRange;
        _selectedTarget = VisibleWorldTargets().Where(v => v.Bounds.IntersectsSegment(from, to))
            .OrderBy(v => v.Center.DistanceSquaredTo(from)).FirstOrDefault()?.Target;
    }

    private void DefaultUnifiedInteraction()
    {
        if (_selectedTarget is { Kind: TargetKind.Entity } target && _world.TryGetEntity(target.Id, out var entity)
            && entity is not null && entity.SpeciesId == "human") PerformGreeting();
        else if (_selectedTarget is null || _selectedTarget.Kind is TargetKind.Food or TargetKind.ResourcePile)
        { BeginCollectUse(); CompleteCollectUse(); }
        else PerformFriendlyInteraction();
    }

    private void OpenUnifiedWheel(string title, string opener, IReadOnlyList<ControlWheelEntry> entries)
    {
        var release = !_controlSettings.Preferences.ToggleWheels
            && opener is ControllerActions.CommandWheel or ControllerActions.BuildWheel;
        _controlWheel.Open(title, opener, entries, release ? "Release " + UnifiedLabel(opener) + " to confirm"
            : UnifiedLabel(ControllerActions.MenuConfirm) + " confirm · " + UnifiedLabel(ControllerActions.CloseMenu) + " back");
        SynchronizeUnifiedContext();
    }

    private void CloseUnifiedWheel(bool confirm)
    {
        if (!UnifiedWheelOpen) return;
        var execute = _controlWheel.Close(confirm);
        SynchronizeUnifiedContext();
        execute?.Invoke();
        SynchronizeUnifiedContext();
    }

    private void OpenBuildWheel()
        => OpenUnifiedWheel("BUILD · select only", ControllerActions.BuildWheel,
            Enumerable.Range(1, 10).Select(i => new ControlWheelEntry(
                i switch { 1 => "1 · House", 2 => "2 · Fireplace", 3 => "3 · Workbench", 4 => "4 · Farm", _ => i + " · Empty" },
                i <= 4 ? () => SelectBuildHotbarSlot(i) : null)).ToArray());

    private void OpenInteractionWheel()
    {
        var captured = _selectedTarget;
        void Run(Action action)
        {
            if (captured is not null && !TargetExists(captured)) { ShowActionFeedback("Target no longer exists."); return; }
            _selectedTarget = captured;
            action();
        }
        var entries = new List<ControlWheelEntry> { new("Default interaction", () => Run(DefaultUnifiedInteraction)) };
        if (captured is { Kind: TargetKind.Entity })
        {
            entries.Add(new("Greet", () => Run(PerformGreeting)));
            entries.Add(new("Feed / pet", () => Run(PerformFriendlyInteraction)));
            entries.Add(new("Follow me", () => Run(() => IssueIndividualOrder(captured.Id, "follow"))));
            entries.Add(new("Stay here", () => Run(() => IssueIndividualOrder(captured.Id, "stay"))));
        }
        if (captured is { Kind: TargetKind.Structure }) entries.Add(new("Rest / wake", () => Run(PerformRestAction)));
        OpenUnifiedWheel("TARGET · " + (captured?.Id ?? "none"), ControllerActions.ContextInteraction, entries);
    }

    private void OpenSocialWheel()
    {
        if (_world.PlayerEntityId is { } id && _world.TryGetEntity(id, out var player)
            && player?.SleepState == SleepState.Sleeping)
        {
            OpenUnifiedWheel("SLEEPING", ControllerActions.SocialWheel, [new("Wake up", PerformRestAction)]);
            return;
        }
        OpenUnifiedWheel("SELF / SOCIAL", ControllerActions.SocialWheel,
        [
            new("Greet selected", _selectedTarget is { Kind: TargetKind.Entity } ? PerformGreeting : null),
            new("Eat carried food", () => ShowActionFeedback(new PlayerResourceService(_world, _speciesCatalog).UseCarriedFood().Message)),
            new("Rest / wake", PerformRestAction),
            new("Inventory", () => ToggleMenu(PlayerMenu.Inventory))
        ]);
    }

    private void OpenCommandWheel()
        => OpenUnifiedWheel($"SQUAD · {_world.SquadMemberIds.Count} recipients", ControllerActions.CommandWheel,
        [
            new("Follow player", () => ShowActionFeedback(_squad!.Execute(SquadOrderKind.FollowPlayer).Reason)),
            new("Hold position", () => ShowActionFeedback(_squad!.Execute(SquadOrderKind.Stop).Reason)),
            new("Move to reticle", () => { _choosingSquadDestination = true; ShowActionFeedback("Choose ground; primary confirms, secondary cancels."); }),
            new("Roster / membership", () => { ToggleMenu(PlayerMenu.Settlement); _settlementTabs.CurrentTab = 2; })
        ]);

    private void BackUnified()
    {
        if (_equipmentActionMenu.Visible) { _equipmentActionMenu.Hide(); SynchronizeUnifiedContext(); return; }
        if (_buildCatalogPanel?.Visible == true) { _buildCatalogPanel.Hide(); RestoreSuspendedBuild(); }
        else if (_quickLoadDialog.Visible) _quickLoadDialog.Hide();
        else if (_npcAnimationTestPanel?.IsOpen == true) _npcAnimationTestPanel.Close();
        else if (_menuState.ActiveMenu is not null) ClosePlayerMenus();
        else if (WorkstationPanelOpen) CloseWorkstation();
        else if (FarmPanelOpen) CloseFarm();
        else if (ConstructionPanelOpen) CloseConstruction();
        else if (PlacementPanelOpen)
        {
            if (_placingHut || _placingFireplace || _placingWorkbench || _placingFarm)
            {
                _placingHut = _placingFireplace = _placingWorkbench = _placingFarm = false;
                _hutPreview.Hide(); _fireplacePreview.Hide(); _workbenchPreview.Hide(); _farmPreview.Hide();
                _selectedBuildSlot = 0; UpdateBuildHotbar();
            }
            else CancelHutPlacement();
        }
        else _choosingSquadDestination = false;
        SynchronizeUnifiedContext();
    }

    private void OpenUnifiedBuildCatalog()
    {
        _suspendedBuildSlot = _selectedBuildSlot;
        if (_buildCatalogPanel is null)
        {
            var panel = new PanelContainer { AnchorLeft = 0.3f, AnchorRight = 0.7f, AnchorTop = 0.20f, AnchorBottom = 0.75f };
            _menuHud.AddChild(panel);
            var content = new VBoxContainer();
            panel.AddChild(content);
            content.AddChild(MenuLabel("BUILD CATALOG"));
            for (var i = 1; i <= 4; i++)
            {
                var slot = i;
                AddMenuButton(content, i switch { 1 => "House", 2 => "Fireplace", 3 => "Workbench", _ => "Farm" }, () =>
                {
                    _suspendedBuildSlot = slot;
                    panel.Hide();
                    RestoreSuspendedBuild();
                });
            }
            AddMenuButton(content, "Back", () => { panel.Hide(); RestoreSuspendedBuild(); });
            _buildCatalogPanel = panel;
        }
        _buildCatalogPanel.Show();
        SynchronizeUnifiedContext();
        _buildCatalogPanel.FindChildren("*", "Button", true, false).OfType<Button>().First().GrabFocus();
    }

    private void RestoreSuspendedBuild()
    {
        SynchronizeUnifiedContext();
        if (_suspendedBuildSlot <= 0 || _controlPause.ManuallyPaused) return;
        var slot = _suspendedBuildSlot;
        _suspendedBuildSlot = 0;
        if (!PlacementPanelOpen) BeginBuildMode();
        SelectBuildHotbarSlot(slot);
        SynchronizeUnifiedContext();
    }

    private void HandleUnifiedMenuAction(string action)
    {
        var tabs = _menuState.ActiveMenu == PlayerMenu.Inventory ? _inventoryTabs
            : _menuState.ActiveMenu == PlayerMenu.Settings ? _settingsTabs : null;
        if (action is ControllerActions.MenuPreviousTab or ControllerActions.MenuNextTab)
        {
            var step = action == ControllerActions.MenuNextTab ? 1 : -1;
            if (tabs is not null)
            {
                var count = tabs.GetTabCount();
                for (var n = 1; n <= count; n++)
                {
                    var index = (tabs.CurrentTab + step * n + count * 2) % count;
                    if (!tabs.IsTabDisabled(index)) { tabs.CurrentTab = index; break; }
                }
            }
            else if (_menuState.ActiveMenu == PlayerMenu.Settlement)
                _settlementTabs.CurrentTab = (_settlementTabs.CurrentTab + step + 3) % 3;
        }
        if (action is ControllerActions.MenuPreviousCategory or ControllerActions.MenuNextCategory)
        {
            var choice = _menuState.ActiveMenu == PlayerMenu.Settings ? _controlCategory
                : _menuState.ActiveMenu == PlayerMenu.Inventory && _inventoryTabs.CurrentTab == 2 ? _itemCategory : null;
            if (choice is not null && choice.ItemCount > 0)
            {
                choice.Selected = (choice.Selected + (action == ControllerActions.MenuNextCategory ? 1 : -1) + choice.ItemCount) % choice.ItemCount;
                if (choice == _controlCategory) FilterUnifiedControls(); else UpdatePlayerMenuContents();
            }
        }
        if (action == ControllerActions.MenuActions && _menuState.ActiveMenu == PlayerMenu.Inventory)
        {
            var focus = GetViewport().GuiGetFocusOwner();
            var slot = _equipmentVisibilityButtons.FirstOrDefault(p => p.Value == focus).Key
                ?? _equipmentUnequipButtons.FirstOrDefault(p => p.Value == focus).Key;
            if (slot is not null && _world.TryGetEntity(PlayerId(), out var actor) && actor is not null
                && actor.EquippedItems.ContainsKey(slot))
            {
                _equipmentActionMenu.Clear();
                _equipmentActionMenu.AddItem("Unequip " + slot, 1);
                _equipmentActionMenu.AddItem(actor.IsEquipmentVisible(slot) ? "Hide visual" : "Show visual", 2);
                _equipmentActionSlot = slot;
                _equipmentActionMenu.PopupCentered();
            }
        }
        if (action == ControllerActions.MenuVisibility && GetViewport().GuiGetFocusOwner() is Button button)
        {
            var match = _equipmentVisibilityButtons.FirstOrDefault(p => p.Value == button);
            if (match.Key is not null && !button.Disabled) TogglePlayerEquipmentVisibility(match.Key);
        }
    }

    private void ExecuteEquipmentMenuAction(long id)
    {
        var slot = _equipmentActionSlot;
        _equipmentActionSlot = null;
        _equipmentActionMenu.Hide();
        if (slot is null) return;
        if (id == 1) ShowActionFeedback(new PlayerEquipmentService(_world).Unequip(slot).Message);
        else TogglePlayerEquipmentVisibility(slot);
        UpdatePlayerMenuContents();
    }

    private string UnifiedLabel(string action, bool? controller = null)
        => string.Join(" / ", _controlSettings.Get(action)
            .Where(b => (controller ?? _inputRouter?.ControllerLast ?? false) == (b.Device is "joypad" or "axis"))
            .Select(UnifiedControlSettings.Label));
}
