using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private Node3D _constructionRoot = null!;
    private MeshInstance3D _hutPreview = null!;
    private Label3D _hutPreviewLabel = null!;
    private MeshInstance3D _fireplacePreview = null!;
    private Label3D _fireplacePreviewLabel = null!;
    private MeshInstance3D _workbenchPreview = null!;
    private Label3D _workbenchPreviewLabel = null!;
    private MeshInstance3D _farmPreview = null!;
    private Label3D _farmPreviewLabel = null!;
    private Control _placementPanel = null!;
    private Label _placementHotbar = null!;
    private readonly List<(PanelContainer Cell, Label Label)> _buildHotbarSlots = [];
    private Label _placementMessage = null!;
    private Control _constructionPanel = null!;
    private Label _constructionSummary = null!;
    private Label _constructionMessage = null!;
    private string? _openConstructionSiteId;
    private bool _placingHut;
    private bool _placingFireplace;
    private bool _placingWorkbench;
    private bool _placingFarm;
    private int _selectedBuildSlot;
    private bool _placementValid;
    private bool _placementGroundHit;
    private Vector3 _placementPosition;
    private readonly Dictionary<WorldTarget, Node3D> _constructionViews = [];

    private bool ConstructionPanelOpen => _constructionPanel is { Visible: true };
    private bool PlacementPanelOpen => _placementPanel is { Visible: true };

    private void ConfigureConstructionGameplay()
    {
        _constructionRoot = new Node3D { Name = "Construction" };
        AddChild(_constructionRoot);
        _hutPreview = new MeshInstance3D
        {
            Name = "HutPreview", Mesh = new BoxMesh { Size = new Vector3(HouseGeometry.Width, 0.15f, HouseGeometry.Depth) }, Visible = false,
            MaterialOverride = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(0.2f, 0.9f, 0.35f, 0.42f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            }
        };
        AddChild(_hutPreview);
        _hutPreviewLabel = new Label3D
        {
            Text = "STARTER HUT", Position = new Vector3(0, 1.2f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 34, OutlineSize = 7, NoDepthTest = true
        };
        _hutPreview.AddChild(_hutPreviewLabel);
        _fireplacePreview = new MeshInstance3D
        {
            Name = "FireplacePreview", Mesh = new CylinderMesh { Height = 0.35f, TopRadius = 0.8f, BottomRadius = 0.8f }, Visible = false,
            MaterialOverride = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(0.2f, 0.9f, 0.35f, 0.55f),
                EmissionEnabled = true, Emission = new Color(1, 0.25f, 0.05f), EmissionEnergyMultiplier = 0.4f
            }
        };
        AddChild(_fireplacePreview);
        _fireplacePreviewLabel = new Label3D
        {
            Text = "FIREPLACE", Position = new Vector3(0, 1.1f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 30, OutlineSize = 7, NoDepthTest = true
        };
        _fireplacePreview.AddChild(_fireplacePreviewLabel);

        _workbenchPreview = new MeshInstance3D
        {
            Name = "WorkbenchPreview", Mesh = new BoxMesh { Size = new Vector3(2, 1.05f, 1.4f) }, Visible = false,
            MaterialOverride = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(0.2f, 0.9f, 0.35f, 0.42f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            }
        };
        AddChild(_workbenchPreview);
        _workbenchPreviewLabel = new Label3D
        {
            Text = "WORKBENCH", Position = new Vector3(0, 1.3f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 30, OutlineSize = 7, NoDepthTest = true
        };
        _workbenchPreview.AddChild(_workbenchPreviewLabel);

        _farmPreview = new MeshInstance3D
        {
            Name = "FarmPreview", Mesh = new BoxMesh { Size = new Vector3(3, 0.12f, 3) }, Visible = false,
            Position = new Vector3(0, 0.06f, 0),
            MaterialOverride = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(0.2f, 0.9f, 0.35f, 0.48f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            }
        };
        AddChild(_farmPreview);
        _farmPreviewLabel = new Label3D
        {
            Text = "FARM", Position = new Vector3(0, 0.9f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 30, OutlineSize = 7, NoDepthTest = true
        };
        _farmPreview.AddChild(_farmPreviewLabel);

        _placementPanel = new PanelContainer
        {
            Name = "BuildHotbar", AnchorLeft = 0.12f, AnchorRight = 0.88f,
            AnchorTop = 1, AnchorBottom = 1, OffsetTop = -178, OffsetBottom = -18,
            MouseFilter = Control.MouseFilterEnum.Ignore, FocusMode = Control.FocusModeEnum.None
        };
        _menuHud.AddChild(_placementPanel);
        ApplyMenuTheme(_placementPanel);
        var hotbarMargin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" }) hotbarMargin.AddThemeConstantOverride($"margin_{side}", 12);
        _placementPanel.AddChild(hotbarMargin);
        var hotbarContent = new VBoxContainer(); hotbarMargin.AddChild(hotbarContent);
        var slotRow = new HBoxContainer { Name = "BuildHotbarSlots", MouseFilter = Control.MouseFilterEnum.Ignore };
        slotRow.AddThemeConstantOverride("separation", 4);
        hotbarContent.AddChild(slotRow);
        for (var slot = 1; slot <= 10; slot++)
        {
            var cell = new PanelContainer
            {
                Name = $"BuildHotbarSlot{slot}", CustomMinimumSize = new Vector2(70, 48),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore
            };
            var label = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 11);
            cell.AddChild(label); slotRow.AddChild(cell);
            _buildHotbarSlots.Add((cell, label));
        }
        _placementHotbar = MenuLabel();
        _placementHotbar.AddThemeFontSizeOverride("font_size", 15);
        hotbarContent.AddChild(_placementHotbar);
        _placementMessage = MenuLabel("Select a build option.");
        _placementMessage.AddThemeColorOverride("font_color", new Color(1, 0.9f, 0.55f));
        hotbarContent.AddChild(_placementMessage);
        _placementPanel.Visible = false;

        var construction = CreateMenuPanel("Construction Site", out _constructionPanel);
        _constructionPanel.AnchorLeft = 0.56f; _constructionPanel.AnchorRight = 0.98f; _constructionPanel.AnchorBottom = 0.52f;
        _constructionPanel.OffsetLeft = 0; _constructionPanel.OffsetRight = -12;
        _constructionSummary = MenuLabel(); construction.AddChild(_constructionSummary);
        _constructionMessage = MenuLabel();
        _constructionMessage.AddThemeColorOverride("font_color", new Color(1, 0.9f, 0.55f));
        construction.AddChild(_constructionMessage);
        var actions = new HBoxContainer(); construction.AddChild(actions);
        AddMenuButton(actions, "Supply 1 wood", () => RunConstructionAction(service => service.SupplyWood(PlayerId(), _openConstructionSiteId!)));
        AddMenuButton(actions, "Apply work", () => RunConstructionAction(service => service.ApplyWork(PlayerId(), _openConstructionSiteId!)));
        _constructionPanel.Visible = false;

        var root = GetNode<VBoxContainer>("Controls/Panel/Scroll/Margin/VBox");
        var button = AddMenuButton(root, "Build mode", BeginBuildMode);
        button.Name = "Construction";
        root.MoveChild(button, 5);
        SyncConstructionPresentation();
    }

    private string PlayerId() => _world.PlayerEntityId ?? string.Empty;

    private void BeginBuildMode()
    {
        if (!_sessionActive || _controlPause.ManuallyPaused) return;
        _suspendedBuildSlot = 0;
        ClosePlayerMenus();
        _placingHut = false;
        _placingFireplace = false;
        _placingWorkbench = false;
        _placingFarm = false;
        _placementPanel.Visible = true;
        _hutPreview.Visible = false;
        _fireplacePreview.Visible = false;
        _workbenchPreview.Visible = false;
        _farmPreview.Visible = false;
        _playerCharacter.SetGameplayInputEnabled(true);
        Input.MouseMode = Input.MouseModeEnum.Captured;
        UpdateBuildHotbar();
        UpdateWorldSelection();
    }

    private void BeginHutPlacement()
    {
        if (!PlacementPanelOpen) BeginBuildMode();
        SelectHutBuildSlot();
    }

    private void BeginFireplacePlacement()
    {
        if (!PlacementPanelOpen) BeginBuildMode();
        SelectFireplaceBuildSlot();
    }

    private void SelectHutBuildSlot()
    {
        if (!PlacementPanelOpen) return;
        _selectedBuildSlot = 1;
        _placingHut = true;
        _placingFireplace = false;
        _placingWorkbench = false;
        _placingFarm = false;
        _hutPreview.Visible = true;
        _fireplacePreview.Visible = false;
        _workbenchPreview.Visible = false;
        _farmPreview.Visible = false;
        UpdateBuildHotbar();
        UpdateHutPreview();
    }

    private void SelectFireplaceBuildSlot()
    {
        if (!PlacementPanelOpen) return;
        _selectedBuildSlot = 2;
        _placingHut = false;
        _placingFireplace = true;
        _placingWorkbench = false;
        _placingFarm = false;
        _hutPreview.Visible = false;
        _fireplacePreview.Visible = true;
        _workbenchPreview.Visible = false;
        _farmPreview.Visible = false;
        UpdateBuildHotbar();
        UpdateFireplacePreview();
    }

    private void SelectWorkbenchBuildSlot()
    {
        if (!PlacementPanelOpen) return;
        _selectedBuildSlot = 3;
        _placingHut = false;
        _placingFireplace = false;
        _placingWorkbench = true;
        _placingFarm = false;
        _hutPreview.Visible = false;
        _fireplacePreview.Visible = false;
        _workbenchPreview.Visible = true;
        _farmPreview.Visible = false;
        UpdateBuildHotbar();
        UpdateWorkbenchPreview();
    }

    private void SelectFarmBuildSlot()
    {
        if (!PlacementPanelOpen) return;
        _selectedBuildSlot = 4;
        _placingHut = false;
        _placingFireplace = false;
        _placingWorkbench = false;
        _placingFarm = true;
        _hutPreview.Visible = false;
        _fireplacePreview.Visible = false;
        _workbenchPreview.Visible = false;
        _farmPreview.Visible = true;
        UpdateBuildHotbar();
        UpdateFarmPlacementPreview();
    }

    private void SelectBuildHotbarSlot(int slot)
    {
        if (slot == 1) { SelectHutBuildSlot(); return; }
        if (slot == 2) { SelectFireplaceBuildSlot(); return; }
        if (slot == 3) { SelectWorkbenchBuildSlot(); return; }
        if (slot == 4) { SelectFarmBuildSlot(); return; }
        if (!PlacementPanelOpen || slot is < 1 or > 10) return;
        _selectedBuildSlot = slot;
        _placingHut = false;
        _placingFireplace = false;
        _placingWorkbench = false;
        _placingFarm = false;
        _hutPreview.Visible = false;
        _fireplacePreview.Visible = false;
        _workbenchPreview.Visible = false;
        _farmPreview.Visible = false;
        UpdateBuildHotbar();
        _placementMessage.Text = $"Hotbar slot {HotbarKey(slot)} is empty.";
    }

    private void UpdateBuildHotbar()
    {
        if (_placementHotbar is null || _buttonBindings is null) return;
        var place = UnifiedLabel(ControllerActions.BuildPlace, false);
        var toggle = UnifiedLabel(ControllerActions.BuildMode, false);
        var padPlace = UnifiedLabel(ControllerActions.BuildPlace, true);
        var padToggle = UnifiedLabel(ControllerActions.BuildMode, true);
        for (var index = 0; index < _buildHotbarSlots.Count; index++)
        {
            var slot = index + 1;
            var title = slot switch { 1 => "Starter hut", 2 => "Fireplace", 3 => "Workbench", 4 => "Farm", _ => "Empty" };
            var label = _buildHotbarSlots[index].Label;
            label.Text = $"{UnifiedLabel("build_slot_" + slot, false)}\n{title}";
            label.AddThemeColorOverride("font_color", slot == _selectedBuildSlot
                ? new Color(0.72f, 1, 0.72f) : new Color(0.9f, 0.93f, 0.98f));
            _buildHotbarSlots[index].Cell.Modulate = slot == _selectedBuildSlot
                ? new Color(0.9f, 1, 0.82f) : Colors.White;
        }
        _placementHotbar.Text = $"1–9, 0 select · {place} place · {UnifiedLabel(ControllerActions.BuildCancel, false)} cancel preview / exit · {toggle} toggle\n" +
            $"Pad: {padToggle} toggle · {UnifiedLabel(ControllerActions.BuildWheel, true)} options · {padPlace} place · " +
            $"{UnifiedLabel(ControllerActions.BuildCancel, true)} cancel preview / exit";
    }

    private static string HotbarKey(int slot) => slot == 10 ? "0" : slot.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private void CancelHutPlacement()
    {
        _placingHut = false;
        _placementPanel.Visible = false;
        _hutPreview.Visible = false;
        _placingFireplace = false;
        _placingWorkbench = false;
        _placingFarm = false;
        _selectedBuildSlot = 0;
        _placementGroundHit = false;
        _fireplacePreview.Visible = false;
        _workbenchPreview.Visible = false;
        _farmPreview.Visible = false;
        if (_placementMessage is not null) _placementMessage.Text = "Select a build option.";
        _playerCharacter.SetGameplayInputEnabled(_sessionActive && _menuState.ActiveMenu is null && !_clock.IsPaused);
    }

    private void CancelFireplacePlacement() => CancelHutPlacement();

    private void CancelPlacement() => CancelHutPlacement();

    private void ConfirmPlacement()
    {
        if (_placingFireplace) ConfirmFireplacePlacement();
        else if (_placingWorkbench) ConfirmWorkbenchPlacement();
        else if (_placingFarm) ConfirmFarmPlacement();
        else ConfirmHutPlacement();
    }

    private void UpdateWorkbenchPreview()
    {
        if (!_placingWorkbench) return;
        var hasGroundPoint = TryGetLookedAtGroundPoint(out _placementPosition);
        _placementGroundHit = hasGroundPoint;
        if (!hasGroundPoint) _placementPosition = new Vector3(_playerCharacter.GlobalPosition.X, 0, _playerCharacter.GlobalPosition.Z);
        var reason = "Aim the reticle at the ground.";
        var valid = hasGroundPoint && CanPlaceWorkbench(_placementPosition, out reason);
        _placementValid = valid;
        _workbenchPreview.GlobalPosition = _placementPosition + new Vector3(0, 0.525f, 0);
        ((StandardMaterial3D)_workbenchPreview.MaterialOverride).AlbedoColor = valid
            ? new Color(0.2f, 0.9f, 0.35f, 0.42f) : new Color(1, 0.2f, 0.16f, 0.42f);
        _workbenchPreviewLabel.Text = valid ? "WORKBENCH · VALID" : "WORKBENCH · BLOCKED";
        _placementMessage.Text = $"Preview: {_placementPosition.X:0}, {_placementPosition.Z:0}\n" +
            (valid ? "Valid · creates an additional workbench." : hasGroundPoint ? reason : "Aim the reticle at the ground.");
    }

    private void UpdateFarmPlacementPreview()
    {
        if (!_placingFarm) return;
        var hasGroundPoint = TryGetLookedAtGroundPoint(out _placementPosition);
        _placementGroundHit = hasGroundPoint;
        if (!hasGroundPoint) _placementPosition = new Vector3(_playerCharacter.GlobalPosition.X, 0, _playerCharacter.GlobalPosition.Z);
        var reason = "Aim the reticle at the ground.";
        var valid = hasGroundPoint && CanPlaceFarm(_placementPosition, out reason);
        _placementValid = valid;
        _farmPreview.GlobalPosition = _placementPosition + new Vector3(0, 0.06f, 0);
        ((StandardMaterial3D)_farmPreview.MaterialOverride).AlbedoColor = valid
            ? new Color(0.2f, 0.9f, 0.35f, 0.48f) : new Color(1, 0.2f, 0.16f, 0.48f);
        _farmPreviewLabel.Text = valid ? "FARM · VALID" : "FARM · BLOCKED";
        _placementMessage.Text = $"Preview: {_placementPosition.X:0}, {_placementPosition.Z:0}\n" +
            (valid ? "Valid · creates an additional farm with one empty plot." : hasGroundPoint ? reason : "Aim the reticle at the ground.");
    }

    private bool CanPlaceWorkbench(Vector3 position, out string reason)
        => CanPlaceFixture(position, 1.5f, out reason);

    private bool CanPlaceFarm(Vector3 position, out string reason)
        => CanPlaceFixture(position, 2f, out reason);

    private bool CanPlaceFixture(Vector3 position, float footprintRadius, out string reason)
    {
        var player = _world.Entities.FirstOrDefault(entity => entity.Id == PlayerId());
        if (player is null) { reason = "The player is unavailable."; return false; }
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Z) || position.Y != 0
            || Math.Abs(position.X) > 58 || Math.Abs(position.Z) > 58)
        { reason = "Move the preview inside valid town terrain."; return false; }
        if (System.Numerics.Vector3.Distance(player.Position, new System.Numerics.Vector3(position.X, 0, position.Z)) > SettlementConstruction.PlacementRange)
        { reason = "Move closer; placement range is 8 m."; return false; }

        if (IsBlockedCell(position.X - footprintRadius, position.X + footprintRadius,
                position.Z - footprintRadius, position.Z + footprintRadius, CurrentWorkbenchPositions()))
        { reason = "The placement overlaps a road, building area or workbench clearance."; return false; }

        var candidate = new System.Numerics.Vector3(position.X, 0, position.Z);
        bool TooClose(System.Numerics.Vector3 other, float requiredDistance)
            => System.Numerics.Vector3.Distance(candidate, other) < requiredDistance;
        if (_world.Structures.Any(item => HouseGeometry.Contains(item, candidate, footprintRadius + .5f))
            || _world.ConstructionSites.Any(item => Math.Abs(item.Position.X - candidate.X) < item.Blueprint.Width / 2f + footprintRadius + .5f
                && Math.Abs(item.Position.Z - candidate.Z) < item.Blueprint.Depth / 2f + footprintRadius + .5f)
            || _world.Stockpiles.Any(item => TooClose(item.Position, footprintRadius + 1.5f))
            || _world.Fireplaces.Any(item => TooClose(item.Position, footprintRadius + 1.5f))
            || _world.Farms.Any(item => TooClose(item.Position, footprintRadius + 1.5f))
            || _world.Workstations.Any(item => TooClose(item.Position, footprintRadius + 1.5f)))
        { reason = "The placement overlaps another settlement object."; return false; }
        reason = "";
        return true;
    }

    private bool RefreshSelectedPlacementPreview()
    {
        if (!_placingHut && !_placingFireplace && !_placingWorkbench && !_placingFarm) return false;
        SyncPlayerPosition();
        if (_placingHut) UpdateHutPreview();
        else if (_placingFireplace) UpdateFireplacePreview();
        else if (_placingWorkbench) UpdateWorkbenchPreview();
        else if (_placingFarm) UpdateFarmPlacementPreview();
        return _placementGroundHit && _placementValid;
    }

    private void ConfirmWorkbenchPlacement()
    {
        if (!RefreshSelectedPlacementPreview()) return;
        if (!CanPlaceWorkbench(_placementPosition, out var reason))
        { _placementMessage.Text = reason; UpdateWorkbenchPreview(); return; }
        var workstationId = NextWorkstationId();
        Workstation workstation;
        try { workstation = _world.AddWorkstation(workstationId, new System.Numerics.Vector3(_placementPosition.X, 0, _placementPosition.Z)); }
        catch (ArgumentException) { _placementMessage.Text = "That workbench identity is already in use."; return; }
        AddWorkbenchView(workstation);
        _navigationRegion.NavigationMesh = CreateNavigationMesh(CurrentWorkbenchPositions(), _world.Structures);
        CancelPlacement();
        _selectedTarget = new(TargetKind.Workstation, workstation.Id);
        _lastActionMessage = "Placed an additional workbench.";
        UpdateWorldSelection();
    }

    private void ConfirmFarmPlacement()
    {
        if (!RefreshSelectedPlacementPreview()) return;
        if (!CanPlaceFarm(_placementPosition, out var reason))
        { _placementMessage.Text = reason; UpdateFarmPlacementPreview(); return; }
        var farmId = NextFarmId();
        Farm farm;
        try
        {
            farm = _world.AddFarm(farmId, new System.Numerics.Vector3(_placementPosition.X, 0, _placementPosition.Z));
            farm.AddPlot(new FarmPlot($"{farmId}-plot-1", capacity: 1));
        }
        catch (ArgumentException) { _placementMessage.Text = "That farm identity is already in use."; return; }
        UpdateFarmPresentation();
        CancelPlacement();
        _selectedTarget = new(TargetKind.Farm, farm.Id);
        _lastActionMessage = "Placed an additional farm with one empty plot.";
        UpdateWorldSelection();
    }

    private string NextWorkstationId()
    {
        var suffix = 1;
        while (_world.Workstations.Any(item => item.Id == $"workbench-{suffix}")) suffix++;
        return $"workbench-{suffix}";
    }

    private string NextFarmId()
    {
        var suffix = 1;
        while (_world.Farms.Any(item => item.Id == $"farm-{suffix}")) suffix++;
        return $"farm-{suffix}";
    }

    private void UpdateHutPreview()
    {
        if (!_placingHut) return;
        var hasGroundPoint = TryGetLookedAtGroundPoint(out _placementPosition);
        _placementGroundHit = hasGroundPoint;
        if (!hasGroundPoint) _placementPosition = new Vector3(_playerCharacter.GlobalPosition.X, 0, _playerCharacter.GlobalPosition.Z);
        var result = hasGroundPoint
            ? new ActorConstructionService(_world, _speciesCatalog, IsHutTerrainBlocked)
                .ValidateHutPlacement(PlayerId(), new System.Numerics.Vector3(_placementPosition.X, 0, _placementPosition.Z))
            : new ConstructionAction(ConstructionActionResult.InvalidTerrain, "Aim the reticle at the ground to place the hut.");
        _placementValid = result.Succeeded;
        _hutPreview.GlobalPosition = _placementPosition;
        ((StandardMaterial3D)_hutPreview.MaterialOverride).AlbedoColor = _placementValid
            ? new Color(0.2f, 0.9f, 0.35f, 0.42f) : new Color(1, 0.2f, 0.16f, 0.42f);
        _hutPreviewLabel.Text = _placementValid ? "STARTER HUT · VALID" : "STARTER HUT · BLOCKED";
        _placementMessage.Text = $"Preview: {_placementPosition.X:0}, {_placementPosition.Z:0}\n" +
            (_placementValid ? $"Valid · requires {SettlementConstruction.HutWood} wood and {SettlementConstruction.HutWork} work." : result.Message);
    }

    private void UpdateFireplacePreview()
    {
        if (!_placingFireplace) return;
        var hasGroundPoint = TryGetLookedAtGroundPoint(out _placementPosition);
        _placementGroundHit = hasGroundPoint;
        if (!hasGroundPoint) _placementPosition = new Vector3(_playerCharacter.GlobalPosition.X, 0, _playerCharacter.GlobalPosition.Z);
        var player = _world.Entities.FirstOrDefault(entity => entity.Id == PlayerId());
        var hasWood = player?.Inventory.GetValueOrDefault(ItemIds.Wood) >= 2;
        var inRange = player is not null && System.Numerics.Vector3.Distance(player.Position,
            new System.Numerics.Vector3(_placementPosition.X, 0, _placementPosition.Z)) <= SettlementConstruction.PlacementRange;
        var blocked = IsBlockedCell(_placementPosition.X - 1, _placementPosition.X + 1, _placementPosition.Z - 1, _placementPosition.Z + 1, CurrentWorkbenchPositions(), _world.Structures)
            || _world.Fireplaces.Any(fire => System.Numerics.Vector3.Distance(fire.Position,
                new System.Numerics.Vector3(_placementPosition.X, 0, _placementPosition.Z)) < 1.5f);
        _placementValid = hasGroundPoint && hasWood && inRange && !blocked;
        _fireplacePreview.GlobalPosition = _placementPosition;
        ((StandardMaterial3D)_fireplacePreview.MaterialOverride).AlbedoColor = _placementValid
            ? new Color(0.2f, 0.9f, 0.35f, 0.55f) : new Color(1, 0.2f, 0.16f, 0.55f);
        _fireplacePreviewLabel.Text = _placementValid ? "FIREPLACE · VALID" : "FIREPLACE · BLOCKED";
        _placementMessage.Text = $"Preview: {_placementPosition.X:0}, {_placementPosition.Z:0}\n" +
            (_placementValid ? "Valid · requires 2 wood." : !hasGroundPoint ? "Aim the reticle at the ground."
                : !hasWood ? "Requires 2 wood." : !inRange ? "Move closer; placement range is 8 m." : "Move to open ground.");
    }

    private bool TryGetLookedAtGroundPoint(out Vector3 snappedPosition)
    {
        snappedPosition = Vector3.Zero;
        var camera = GetViewport().GetCamera3D();
        if (camera is null) return false;
        var center = GetViewport().GetVisibleRect().Size * 0.5f;
        var origin = camera.ProjectRayOrigin(center);
        var direction = camera.ProjectRayNormal(center);
        if (!TryIntersectGroundPlane(origin, direction, 0, out var point)) return false;
        snappedPosition = new Vector3(Mathf.Round(point.X), 0, Mathf.Round(point.Z));
        return true;
    }

    internal static bool TryIntersectGroundPlane(Vector3 origin, Vector3 direction, float groundHeight, out Vector3 point)
    {
        point = Vector3.Zero;
        if (Mathf.Abs(direction.Y) < 0.001f || direction.Y >= 0) return false;
        var distance = (groundHeight - origin.Y) / direction.Y;
        if (!float.IsFinite(distance) || distance < 0) return false;
        point = origin + direction * distance;
        return float.IsFinite(point.X) && float.IsFinite(point.Z);
    }

    private bool IsHutTerrainBlocked(System.Numerics.Vector3 position)
        => IsBlockedCell(position.X - HouseGeometry.Width / 2, position.X + HouseGeometry.Width / 2,
            position.Z - HouseGeometry.Depth / 2, position.Z + HouseGeometry.Depth / 2,
            CurrentWorkbenchPositions());

    private void ConfirmHutPlacement()
    {
        if (!RefreshSelectedPlacementPreview()) return;
        var result = new ActorConstructionService(_world, _speciesCatalog, IsHutTerrainBlocked)
            .PlaceHut(PlayerId(), "hut-site-1", new System.Numerics.Vector3(_placementPosition.X, 0, _placementPosition.Z));
        if (!result.Succeeded) { _placementMessage.Text = result.Message; UpdateHutPreview(); return; }
        CancelHutPlacement();
        SyncConstructionPresentation();
        _selectedTarget = new(TargetKind.ConstructionSite, result.CreatedId!);
        _lastActionMessage = result.Message;
        UpdateWorldSelection();
    }

    private void ConfirmFireplacePlacement()
    {
        if (!RefreshSelectedPlacementPreview()) return;
        var fireplaceId = $"fireplace-{_world.Fireplaces.Count + 1}";
        var result = new ActorConstructionService(_world, _speciesCatalog, position =>
            IsBlockedCell(position.X - 1, position.X + 1, position.Z - 1, position.Z + 1, CurrentWorkbenchPositions(), _world.Structures))
            .PlaceFireplace(PlayerId(), fireplaceId, new System.Numerics.Vector3(_placementPosition.X, 0, _placementPosition.Z));
        if (!result.Succeeded) { _placementMessage.Text = result.Message; UpdateFireplacePreview(); return; }
        CancelFireplacePlacement();
        RebuildFireplaceViews();
        _selectedTarget = new(TargetKind.Fireplace, result.CreatedId!);
        _lastActionMessage = result.Message;
        UpdateWorldSelection();
    }

    private void OpenConstructionSite(string id)
    {
        var site = _world.ConstructionSites.FirstOrDefault(item => item.Id == id);
        var player = _world.Entities.FirstOrDefault(item => item.Id == _world.PlayerEntityId);
        if (site is null || player is null) { ShowActionFeedback("The construction site is unavailable."); return; }
        if (System.Numerics.Vector3.Distance(player.Position, site.Position) > SettlementConstruction.InteractionRange)
        { ShowActionFeedback("Move within 2 m of the construction site."); return; }
        CloseFarm(); CloseWorkstation();
        _openConstructionSiteId = id;
        _constructionMessage.Text = "Supply materials, then apply construction work.";
        _constructionPanel.Visible = true;
        _playerCharacter.SetGameplayInputEnabled(false);
        UpdateConstructionPanel();
    }

    private void CloseConstruction()
    {
        if (_constructionPanel is null) return;
        _constructionPanel.Visible = false;
        _openConstructionSiteId = null;
        _playerCharacter.SetGameplayInputEnabled(_sessionActive && _menuState.ActiveMenu is null
            && !WorkstationPanelOpen && !FarmPanelOpen && !PlacementPanelOpen && !_clock.IsPaused);
    }

    private void RunConstructionAction(Func<ActorConstructionService, ConstructionAction> action)
    {
        SyncPlayerPosition();
        var result = action(new ActorConstructionService(_world, _speciesCatalog, IsHutTerrainBlocked));
        _constructionMessage.Text = result.Message; _lastActionMessage = result.Message;
        if (result.CreatedId is not null)
        {
            CloseConstruction();
            _selectedTarget = new(TargetKind.Structure, result.CreatedId);
            _navigationRegion.NavigationMesh = CreateNavigationMesh(CurrentWorkbenchPositions(), _world.Structures);
        }
        SyncConstructionPresentation(); UpdateConstructionPanel(); UpdatePlayerMenuContents(); UpdateWorldSelection();
    }

    private void UpdateConstructionPanel()
    {
        if (!ConstructionPanelOpen || _openConstructionSiteId is null) return;
        var site = _world.ConstructionSites.FirstOrDefault(item => item.Id == _openConstructionSiteId);
        if (site is null) { CloseConstruction(); return; }
        var blueprint = site.Blueprint;
        _constructionSummary.Text = $"SITE: {site.State}\nWood: {blueprint.GetProgress(ItemIds.Wood)}/{SettlementConstruction.HutWood}\nWork: {blueprint.WorkProgress}/{blueprint.WorkRequired}";
    }

    private void SyncConstructionPresentation()
    {
        if (_constructionRoot is null) return;
        foreach (var child in _constructionRoot.GetChildren()) child.QueueFree();
        _constructionViews.Clear();
        foreach (var site in _world.ConstructionSites)
        {
            var node = BuildSiteView(site); _constructionRoot.AddChild(node);
            _constructionViews.Add(new(TargetKind.ConstructionSite, site.Id), node);
        }
        foreach (var structure in _world.Structures)
        {
            var node = BuildHutView(structure); _constructionRoot.AddChild(node);
            _constructionViews.Add(new(TargetKind.Structure, structure.Id), node);
        }
    }

    private static Node3D BuildSiteView(SettlementConstructionSite site)
    {
        var root = new Node3D { Name = site.Id, Position = new Vector3(site.Position.X, site.Position.Y, site.Position.Z) };
        var progress = (site.Blueprint.GetProgress(ItemIds.Wood) + site.Blueprint.WorkProgress) /
            (float)(SettlementConstruction.HutWood + SettlementConstruction.HutWork);
        var material = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.35f, 0.13f) };
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(site.Blueprint.Width, 0.16f, site.Blueprint.Depth) }, Position = new Vector3(0, 0.08f, 0), MaterialOverride = material });
        foreach (var x in new[] { -site.Blueprint.Width / 2f + .3f, site.Blueprint.Width / 2f - .3f })
        foreach (var z in new[] { -site.Blueprint.Depth / 2f + .3f, site.Blueprint.Depth / 2f - .3f })
            root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.2f, 2.8f * Math.Max(0.12f, progress), 0.2f) },
                Position = new Vector3(x, 0.2f + 1.4f * Math.Max(0.12f, progress), z), MaterialOverride = material });
        root.AddChild(new Label3D { Text = $"HUT SITE · {site.State}", Position = new Vector3(0, 3.3f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 34, OutlineSize = 7, NoDepthTest = true });
        return root;
    }

    private Node3D BuildHutView(Structure structure)
    {
        var root = new Node3D { Name = structure.Id, Position = new Vector3(structure.Position.X, structure.Position.Y, structure.Position.Z),
            Rotation = new Vector3(0, structure.FacingQuarterTurns * Mathf.Pi / 2, 0) };
        var variant = structure.Id.Aggregate(0u, (hash, letter) => unchecked(hash * 31 + letter)) % 4;
        var wall = new StandardMaterial3D { AlbedoColor = new Color(.72f + variant * .035f, .65f + variant * .025f, .49f + variant * .04f), Roughness = .95f };
        var roof = new StandardMaterial3D { AlbedoColor = new Color(.28f + variant * .025f, .13f + variant * .025f, .09f + variant * .025f), Roughness = .95f };
        var timber = new StandardMaterial3D { AlbedoColor = new Color(.24f, .15f, .08f), Roughness = .9f };
        void Part(Vector3 position, Vector3 size, Material material) => root.AddChild(new MeshInstance3D
            { Mesh = new BoxMesh { Size = size }, Position = position, MaterialOverride = material });
        var collision = new StaticBody3D { Name = "BuildingCollision", CollisionLayer = 2, CollisionMask = 1 };
        root.AddChild(collision);
        void SolidPart(string name, Vector3 position, Vector3 size) => collision.AddChild(new CollisionShape3D
        {
            Name = name, Position = position, Shape = new BoxShape3D { Size = size }
        });
        foreach (var part in HouseGeometry.SolidParts(structure.IsClosed))
        {
            Part(new(part.Center.X, part.Center.Y, part.Center.Z), new(part.Size.X, part.Size.Y, part.Size.Z),
                part.Name is "Floor" or "Door" ? timber : wall);
            SolidPart(part.Name, new(part.Center.X, part.Center.Y, part.Center.Z), new(part.Size.X, part.Size.Y, part.Size.Z));
        }
        var roofAngle = Mathf.Atan2(1.8f, 4.4f);
        foreach (var side in new[] { -1, 1 })
            root.AddChild(new MeshInstance3D { Name = side < 0 ? "RoofLeft" : "RoofRight",
                Mesh = new BoxMesh { Size = new Vector3(Mathf.Sqrt(4.4f * 4.4f + 1.8f * 1.8f), .18f, 8.8f) },
                Position = new Vector3(side * 2.2f, 4.2f, 0), Rotation = new Vector3(0, 0, -side * roofAngle), MaterialOverride = roof });
        foreach (var side in new[] { -1, 1 })
        {
            var gable = new ImmediateMesh();
            gable.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            gable.SurfaceSetNormal(new Vector3(0, 0, side));
            gable.SurfaceAddVertex(new Vector3(-4 * side, 3.3f, 4 * side));
            gable.SurfaceAddVertex(new Vector3(0, 5.1f, 4 * side));
            gable.SurfaceAddVertex(new Vector3(4 * side, 3.3f, 4 * side));
            gable.SurfaceEnd();
            root.AddChild(new MeshInstance3D { Mesh = gable, MaterialOverride = wall });
        }
        if (!structure.IsClosed)
            Part(new(-2.5f, HouseGeometry.DoorHeight / 2, 4.025f), new(2.4f, HouseGeometry.DoorHeight, .06f), timber);
        foreach (var x in new[] { -3.82f, 3.82f }) foreach (var z in new[] { -3.82f, 3.82f })
            Part(new(x, HouseGeometry.WallHeight / 2, z), new(.24f, HouseGeometry.WallHeight, .24f), timber);
        foreach (var x in new[] { -1.27f, 1.27f })
            Part(new(x, HouseGeometry.DoorHeight / 2, 4.025f), new(.14f, HouseGeometry.DoorHeight, .1f), timber);
        Part(new(0, 2.7f, 4.025f), new(2.7f, .2f, .1f), timber);
        var glass = new StandardMaterial3D { AlbedoColor = new Color(.18f, .31f, .33f), Roughness = .35f };
        foreach (var side in new[] { -1, 1 })
        {
            Part(new(side * 4.01f, 1.85f, -.7f), new(.04f, 1.1f, 1.5f), timber);
            Part(new(side * 4.04f, 1.85f, -.7f), new(.04f, .86f, 1.22f), glass);
            Part(new(side * 4.07f, 1.85f, -.7f), new(.04f, .08f, 1.22f), timber);
            Part(new(side * 4.07f, 1.85f, -.7f), new(.04f, .86f, .08f), timber);
        }
        // Keep the bed inside the open footprint so the shelter communicates its usable rest point.
        var bedFrame = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.16f, 0.08f) };
        var bedding = new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.78f, 0.86f) };
        Part(new(-2.5f, .42f, -1.7f), new(1.45f, .25f, 2.25f), bedFrame);
        Part(new(-2.5f, .62f, -1.7f), new(1.3f, .18f, 2.1f), bedding);
        Part(new(-2.5f, .78f, -2.5f), new(1.3f, .22f, .28f), bedding);
        var title = "HOUSE";
        if (structure.ResidentId is { } resident && _world.TryGetEntity(resident, out var npc) && npc is not null)
            title = StarterNpcPresentationCatalog.TryGet(resident, npc.Sex, out var presentation) && presentation is not null
                ? $"{presentation.DisplayName}'s home" : "Resident home";
        root.AddChild(new Label3D { Text = $"{title} · DOOR {(structure.IsLocked ? "LOCKED" : structure.IsClosed ? "CLOSED" : "OPEN")}",
            Position = new Vector3(0, 5.6f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 28, OutlineSize = 7 });
        return root;
    }
}
