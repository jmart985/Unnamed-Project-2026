using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private Node3D _constructionRoot = null!;
    private MeshInstance3D _hutPreview = null!;
    private Label3D _hutPreviewLabel = null!;
    private Control _placementPanel = null!;
    private Label _placementMessage = null!;
    private Control _constructionPanel = null!;
    private Label _constructionSummary = null!;
    private Label _constructionMessage = null!;
    private string? _openConstructionSiteId;
    private bool _placingHut;
    private bool _placementValid;
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
            Name = "HutPreview", Mesh = new BoxMesh { Size = new Vector3(4, 0.15f, 4) }, Visible = false,
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

        var placement = CreateMenuPanel("Place Starter Hut", out _placementPanel);
        _placementPanel.AnchorLeft = 0.56f; _placementPanel.AnchorRight = 0.98f; _placementPanel.AnchorBottom = 0.4f;
        _placementPanel.OffsetLeft = 0; _placementPanel.OffsetRight = -12;
        _placementMessage = MenuLabel(); placement.AddChild(_placementMessage);
        var placementActions = new HBoxContainer(); placement.AddChild(placementActions);
        AddMenuButton(placementActions, "Confirm site", ConfirmHutPlacement);
        AddMenuButton(placementActions, "Cancel placement", CancelHutPlacement);
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
        var button = AddMenuButton(root, "Place starter hut", BeginHutPlacement);
        button.Name = "Construction";
        root.MoveChild(button, 5);
        SyncConstructionPresentation();
    }

    private string PlayerId() => _world.PlayerEntityId ?? string.Empty;

    private void BeginHutPlacement()
    {
        ClosePlayerMenus();
        _placingHut = true;
        _placementPanel.Visible = true;
        _hutPreview.Visible = true;
        _playerCharacter.SetGameplayInputEnabled(false);
        UpdateHutPreview();
    }

    private void CancelHutPlacement()
    {
        _placingHut = false;
        _placementPanel.Visible = false;
        _hutPreview.Visible = false;
        _playerCharacter.SetGameplayInputEnabled(_sessionActive && _menuState.ActiveMenu is null && !_clock.IsPaused);
    }

    private void UpdateHutPreview()
    {
        if (!_placingHut) return;
        var forward = -_playerCharacter.GlobalBasis.Z;
        _placementPosition = new Vector3(Mathf.Round(_playerCharacter.GlobalPosition.X + forward.X * 5), 0,
            Mathf.Round(_playerCharacter.GlobalPosition.Z + forward.Z * 5));
        var service = new ActorConstructionService(_world, _speciesCatalog, IsHutTerrainBlocked);
        var result = service.ValidateHutPlacement(PlayerId(), new System.Numerics.Vector3(_placementPosition.X, 0, _placementPosition.Z));
        _placementValid = result.Succeeded;
        _hutPreview.GlobalPosition = _placementPosition;
        ((StandardMaterial3D)_hutPreview.MaterialOverride).AlbedoColor = _placementValid
            ? new Color(0.2f, 0.9f, 0.35f, 0.42f) : new Color(1, 0.2f, 0.16f, 0.42f);
        _hutPreviewLabel.Text = _placementValid ? "STARTER HUT · VALID" : "STARTER HUT · BLOCKED";
        _placementMessage.Text = $"Preview: {_placementPosition.X:0}, {_placementPosition.Z:0}\n" +
            (_placementValid ? $"Valid · requires {SettlementConstruction.HutWood} wood and {SettlementConstruction.HutWork} work." : result.Message);
    }

    private bool IsHutTerrainBlocked(System.Numerics.Vector3 position)
        => IsBlockedCell((int)position.X - 2, (int)position.X + 2, (int)position.Z - 2, (int)position.Z + 2);

    private void ConfirmHutPlacement()
    {
        SyncPlayerPosition();
        var result = new ActorConstructionService(_world, _speciesCatalog, IsHutTerrainBlocked)
            .PlaceHut(PlayerId(), "hut-site-1", new System.Numerics.Vector3(_placementPosition.X, 0, _placementPosition.Z));
        if (!result.Succeeded) { _placementMessage.Text = result.Message; UpdateHutPreview(); return; }
        CancelHutPlacement();
        SyncConstructionPresentation();
        _selectedTarget = new(TargetKind.ConstructionSite, result.CreatedId!);
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
        if (result.CreatedId is not null) { CloseConstruction(); _selectedTarget = new(TargetKind.Structure, result.CreatedId); }
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
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(4, 0.16f, 4) }, Position = new Vector3(0, 0.08f, 0), MaterialOverride = material });
        foreach (var x in new[] { -1.7f, 1.7f }) foreach (var z in new[] { -1.7f, 1.7f })
            root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.2f, 2.8f * Math.Max(0.12f, progress), 0.2f) },
                Position = new Vector3(x, 0.2f + 1.4f * Math.Max(0.12f, progress), z), MaterialOverride = material });
        root.AddChild(new Label3D { Text = $"HUT SITE · {site.State}", Position = new Vector3(0, 3.3f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 34, OutlineSize = 7, NoDepthTest = true });
        return root;
    }

    private static Node3D BuildHutView(Structure structure)
    {
        var root = new Node3D { Name = structure.Id, Position = new Vector3(structure.Position.X, structure.Position.Y, structure.Position.Z) };
        var wall = new StandardMaterial3D { AlbedoColor = new Color(0.56f, 0.32f, 0.14f), Roughness = 0.9f };
        var roof = new StandardMaterial3D { AlbedoColor = new Color(0.24f, 0.12f, 0.08f) };
        void Part(Vector3 position, Vector3 size, Material material) => root.AddChild(new MeshInstance3D
            { Mesh = new BoxMesh { Size = size }, Position = position, MaterialOverride = material });
        Part(new(0, 0.1f, 0), new(4, 0.2f, 4), wall);
        Part(new(-1.9f, 1.5f, 0), new(0.2f, 3, 4), wall); Part(new(1.9f, 1.5f, 0), new(0.2f, 3, 4), wall);
        Part(new(0, 1.5f, -1.9f), new(4, 3, 0.2f), wall);
        Part(new(-1.25f, 1.5f, 1.9f), new(1.5f, 3, 0.2f), wall); Part(new(1.25f, 1.5f, 1.9f), new(1.5f, 3, 0.2f), wall);
        Part(new(0, 3.15f, 0), new(4.5f, 0.3f, 4.5f), roof);
        if (structure.IsClosed) Part(new(0, 1.2f, 1.88f), new(1, 2.4f, 0.12f), roof);
        root.AddChild(new Label3D { Text = structure.IsClosed ? "STARTER HUT · DOOR CLOSED" : "STARTER HUT · DOOR OPEN",
            Position = new Vector3(0, 3.8f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 34, OutlineSize = 7, NoDepthTest = true });
        return root;
    }
}
