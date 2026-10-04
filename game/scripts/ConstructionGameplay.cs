using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private Node3D _constructionViews = null!;
    private MeshInstance3D _hutPreview = null!;
    private Control _constructionPanel = null!;
    private Label _constructionSummary = null!;
    private Label _constructionMessage = null!;
    private string? _openConstructionSiteId;
    private bool _hutPlacementActive;

    private bool ConstructionPanelOpen => _constructionPanel is { Visible: true };

    private void ConfigureConstructionGameplay()
    {
        _constructionViews = new Node3D { Name = "ConstructionViews" };
        AddChild(_constructionViews);
        _hutPreview = CreateHutMesh("PlacementPreview", true);
        _hutPreview.Visible = false;
        AddChild(_hutPreview);

        var content = CreateMenuPanel("Settlement Construction", out _constructionPanel);
        _constructionPanel.AnchorLeft = 0.56f;
        _constructionPanel.AnchorRight = 0.98f;
        _constructionPanel.AnchorBottom = 0.52f;
        _constructionPanel.OffsetLeft = 0;
        _constructionPanel.OffsetRight = -12;
        _constructionSummary = MenuLabel("One starter hut · footprint 4 m × 4 m · 8 wood");
        content.AddChild(_constructionSummary);
        _constructionMessage = MenuLabel("Choose a clear location within 12 m.");
        content.AddChild(_constructionMessage);
        var actions = new GridContainer { Columns = 2 };
        content.AddChild(actions);
        AddMenuButton(actions, "Place starter hut", BeginHutPlacement);
        AddMenuButton(actions, "Load carried wood", () => RunConstructionAction(service => service.LoadWood(_openConstructionSiteId!)));
        AddMenuButton(actions, "Apply build work", () => RunConstructionAction(service => service.ApplyWork(_openConstructionSiteId!)));
        _constructionPanel.Visible = false;
        var buildButton = new Button { Name = "BuildHut", Text = "Build Hut", Position = new Vector2(20, 48), Size = new Vector2(130, 38), TooltipText = "Place the starter hut" };
        buildButton.Pressed += () =>
        {
            _constructionPanel.Visible = true;
            _openConstructionSiteId = null;
            _constructionPanel.GetNode<Control>(".").Visible = true;
            _playerCharacter.SetGameplayInputEnabled(false);
        };
        _menuHud.AddChild(buildButton);
        UpdateConstructionPresentation();
    }

    private static MeshInstance3D CreateHutMesh(string name, bool preview)
    {
        var root = new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh { Size = new Vector3(3.8f, 2.5f, 3.8f) },
            Position = new Vector3(0, 1.25f, 0),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = preview ? new Color(0.4f, 0.8f, 0.45f, 0.45f) : new Color(0.43f, 0.28f, 0.17f),
                Transparency = preview ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
                Roughness = 0.9f
            },
            CastShadow = preview ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On
        };
        return root;
    }

    private void BeginHutPlacement()
    {
        CloseConstructionPanel();
        _hutPlacementActive = true;
        _hutPreview.Visible = true;
        _constructionMessage.Text = "Move the pointer to a clear location. Click to place; Escape cancels.";
        _constructionPanel.Visible = true;
        _playerCharacter.SetGameplayInputEnabled(false);
    }

    private void UpdateHutPreview()
    {
        if (!_hutPlacementActive) return;
        var camera = GetViewport().GetCamera3D();
        if (camera is null) return;
        var origin = camera.ProjectRayOrigin(GetViewport().GetMousePosition());
        var direction = camera.ProjectRayNormal(GetViewport().GetMousePosition());
        var point = new Plane(Vector3.Up, 0).IntersectsRay(origin, direction);
        if (point is null) return;
        _hutPreview.GlobalPosition = new Vector3(Mathf.Round(point.Value.X), 0, Mathf.Round(point.Value.Z));
    }

    private void HandleConstructionInput(InputEvent input)
    {
        if (!_hutPlacementActive) return;
        if (input is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            CancelHutPlacement();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            SyncPlayerPosition();
            var result = new PlayerConstructionService(_world, _speciesCatalog)
                .PlaceStarterHut(new System.Numerics.Vector3(_hutPreview.GlobalPosition.X, 0, _hutPreview.GlobalPosition.Z), IsPlacementNavigationClear());
            _constructionMessage.Text = result.Message;
            _lastActionMessage = result.Message;
            if (result.Succeeded)
            {
                CancelHutPlacement();
                UpdateConstructionPresentation();
            }
            GetViewport().SetInputAsHandled();
        }
    }

    private bool IsPlacementNavigationClear()
    {
        var map = _navigationRegion.GetNavigationMap();
        var player = _playerCharacter.GlobalPosition;
        var destination = _hutPreview.GlobalPosition;
        var path = NavigationServer3D.MapGetPath(map, player, destination, true);
        return path.Length > 0 && path[^1].DistanceTo(destination) <= 2f;
    }

    private void OpenConstructionSite(string id)
    {
        var site = _world.ConstructionSites.FirstOrDefault(item => item.Id == id);
        var player = _world.Entities.FirstOrDefault(entity => entity.Id == _world.PlayerEntityId);
        if (site is null || player is null || System.Numerics.Vector3.Distance(player.Position, site.Position) > SettlementConstruction.InteractionRange)
        { ShowActionFeedback("Move within 2 m of the construction site."); return; }
        CloseFarm();
        CloseWorkstation();
        _openConstructionSiteId = id;
        _constructionPanel.Visible = true;
        _playerCharacter.SetGameplayInputEnabled(false);
        UpdateConstructionPanel();
    }

    private void RunConstructionAction(Func<PlayerConstructionService, ConstructionAction> action)
    {
        SyncPlayerPosition();
        var result = action(new PlayerConstructionService(_world, _speciesCatalog));
        _constructionMessage.Text = result.Message;
        _lastActionMessage = result.Message;
        UpdateConstructionPanel();
        UpdateConstructionPresentation();
        UpdateWorldSelection();
    }

    private void UpdateConstructionPanel()
    {
        if (!ConstructionPanelOpen || _openConstructionSiteId is null)
        {
            if (ConstructionPanelOpen && !_hutPlacementActive)
                _constructionSummary.Text = "One starter hut · footprint 4 m × 4 m · 8 wood";
            return;
        }
        var site = _world.ConstructionSites.FirstOrDefault(item => item.Id == _openConstructionSiteId);
        if (site is null) { _openConstructionSiteId = null; _constructionSummary.Text = "The hut is complete."; return; }
        _constructionSummary.Text = $"STARTER HUT · {site.Blueprint.Width} m × {site.Blueprint.Depth} m\nWood: {site.Blueprint.GetProgress(ItemIds.Wood)}/{site.Blueprint.RequiredMaterials[ItemIds.Wood]}";
    }

    private void UpdateConstructionPresentation()
    {
        if (_constructionViews is null) return;
        foreach (var child in _constructionViews.GetChildren()) child.QueueFree();
        foreach (var site in _world.ConstructionSites)
        {
            var frame = CreateHutMesh($"Site_{site.Id}", true);
            frame.Position = new Vector3(site.Position.X, 0, site.Position.Z);
            ((StandardMaterial3D)frame.MaterialOverride).AlbedoColor = new Color(0.85f, 0.67f, 0.28f, 0.65f);
            _constructionViews.AddChild(frame);
            var label = new Label3D { Text = $"HUT SITE · {site.Blueprint.GetProgress(ItemIds.Wood)}/{site.Blueprint.RequiredMaterials[ItemIds.Wood]} WOOD", Position = new Vector3(0, 3, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 30, OutlineSize = 7, NoDepthTest = true };
            frame.AddChild(label);
        }
        foreach (var structure in _world.Structures.Where(item => item.Type == StructureType.Hut))
        {
            var hut = CreateHutMesh($"Hut_{structure.Id}", false);
            hut.Position = new Vector3(structure.Position.X, 0, structure.Position.Z);
            var door = new MeshInstance3D
            {
                Name = "Doorway",
                Mesh = new BoxMesh { Size = new Vector3(1, 1.8f, 0.12f) },
                Position = new Vector3(0, 0.9f, 1.96f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.25f, 0.14f, 0.08f) }
            };
            hut.AddChild(door);
            var label = new Label3D { Text = structure.IsClosed ? "HUT · CLOSED" : "HUT · OPEN", Position = new Vector3(0, 3, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 30, OutlineSize = 7, NoDepthTest = true };
            hut.AddChild(label);
            _constructionViews.AddChild(hut);
        }
        _hutPreview.Visible = _hutPlacementActive;
    }

    private void CloseConstructionPanel()
    {
        if (_constructionPanel is null) return;
        _constructionPanel.Visible = false;
        _openConstructionSiteId = null;
    }

    private void CancelHutPlacement()
    {
        _hutPlacementActive = false;
        _hutPreview.Visible = false;
        CloseConstructionPanel();
        _playerCharacter.SetGameplayInputEnabled(_sessionActive && _menuState.ActiveMenu is null && !WorkstationPanelOpen && !FarmPanelOpen && !_clock.IsPaused);
    }
}