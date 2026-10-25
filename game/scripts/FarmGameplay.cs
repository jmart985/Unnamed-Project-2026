using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private Node3D _farmView = null!;
    private MeshInstance3D _farmSoil = null!;
    private Node3D _farmCrop = null!;
    private Label3D _farmLabel = null!;
    private sealed record FarmPresentation(Node3D Root, MeshInstance3D Soil, Node3D Crop, Label3D Label);
    private readonly Dictionary<string, FarmPresentation> _farmViews = new(StringComparer.Ordinal);
    private Control _farmPanel = null!;
    private Label _farmSummary = null!;
    private Label _farmMessage = null!;
    private string? _openFarmId;
    private string? _openPlotId;

    private bool FarmPanelOpen => _farmPanel is { Visible: true };

    private void ConfigureFarmGameplay()
    {
        var starterView = CreateFarmPresentation("Farm");
        _farmView = starterView.Root;
        _farmSoil = starterView.Soil;
        _farmCrop = starterView.Crop;
        _farmLabel = starterView.Label;
        _farmViews.Add("farm-1", starterView);

        var content = CreateMenuPanel("Farm Plot", out _farmPanel);
        _farmPanel.AnchorLeft = 0.56f;
        _farmPanel.AnchorRight = 0.98f;
        _farmPanel.AnchorBottom = 0.52f;
        _farmPanel.OffsetLeft = 0;
        _farmPanel.OffsetRight = -12;
        _farmSummary = MenuLabel();
        content.AddChild(_farmSummary);
        _farmMessage = MenuLabel("Select an action.");
        _farmMessage.AddThemeColorOverride("font_color", new Color(1, 0.9f, 0.55f));
        content.AddChild(_farmMessage);
        var actions = new GridContainer { Columns = 3 };
        content.AddChild(actions);
        AddMenuButton(actions, "Plant 1 plant", () => RunFarmAction(service => service.Plant(_openFarmId!, _openPlotId!)));
        AddMenuButton(actions, "Apply growth", () => RunFarmAction(service => service.AdvanceGrowth(_openFarmId!, _openPlotId!)));
        AddMenuButton(actions, "Harvest food", () => RunFarmAction(service => service.Harvest(_openFarmId!, _openPlotId!)));
        _farmPanel.Visible = false;
        UpdateFarmPresentation();
    }

    private void OpenFarm(string id)
    {
        var farm = _world.Farms.FirstOrDefault(item => item.Id == id);
        var plot = farm?.Plots.FirstOrDefault();
        if (farm is null || plot is null) { ShowActionFeedback("The farm plot is unavailable."); return; }
        var player = _world.Entities.FirstOrDefault(entity => entity.Id == _world.PlayerEntityId);
        if (player is null || System.Numerics.Vector3.Distance(player.Position, farm.Position) > PlayerFarmService.InteractionRange)
        { ShowActionFeedback("Move within 2 m of the farm plot."); return; }
        CloseWorkstation();
        _openFarmId = id;
        _openPlotId = plot.Id;
        _farmMessage.Text = "Plant, tend, and harvest the selected plot.";
        _farmPanel.Visible = true;
        _playerCharacter.SetGameplayInputEnabled(false);
        UpdateFarmPanel();
    }

    private void CloseFarm()
    {
        if (_farmPanel is null) return;
        _farmPanel.Visible = false;
        _openFarmId = _openPlotId = null;
        _playerCharacter.SetGameplayInputEnabled(_sessionActive && _menuState.ActiveMenu is null && !WorkstationPanelOpen && !_clock.IsPaused);
        UpdateWorldSelection();
    }

    private void RunFarmAction(Func<PlayerFarmService, FarmAction> action)
    {
        SyncPlayerPosition();
        var result = action(new PlayerFarmService(_world, _speciesCatalog));
        _farmMessage.Text = result.Message;
        _lastActionMessage = result.Message;
        UpdateFarmPanel();
        UpdateFarmPresentation();
        UpdatePlayerMenuContents();
        UpdateWorldSelection();
    }

    private void UpdateFarmPanel()
    {
        if (!FarmPanelOpen || _openFarmId is null || _openPlotId is null) return;
        var plot = _world.Farms.FirstOrDefault(item => item.Id == _openFarmId)?.Plots.FirstOrDefault(item => item.Id == _openPlotId);
        if (plot is null) { CloseFarm(); return; }
        var state = !plot.IsPlanted ? "Empty" : plot.IsMature ? "Mature · ready to harvest" : $"Growing · {plot.Growth}/{plot.Crop!.GrowthRequired}";
        _farmSummary.Text = $"PLOT STATUS: {state}\nPlanting cost: 1 carried plant\nHarvest: {SettlementCrops.HarvestFood} food after {SettlementCrops.GrowthSteps} growth steps";
    }

    private void UpdateFarmPresentation()
    {
        if (_farmView is null) return;
        var liveFarmIds = _world.Farms.Select(farm => farm.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var (id, view) in _farmViews.ToArray())
        {
            if (liveFarmIds.Contains(id)) continue;
            view.Root.Visible = false;
            if (id == "farm-1") continue;
            RemoveChild(view.Root);
            view.Root.QueueFree();
            _farmViews.Remove(id);
        }

        foreach (var farm in _world.Farms)
        {
            if (!_farmViews.TryGetValue(farm.Id, out var view))
            {
                view = CreateFarmPresentation(farm.Id);
                _farmViews.Add(farm.Id, view);
            }
            var plot = farm.Plots.FirstOrDefault();
            view.Root.Visible = plot is not null;
            if (plot is null) continue;
            view.Root.Position = new Vector3(farm.Position.X, farm.Position.Y, farm.Position.Z);
            view.Crop.Visible = plot.IsPlanted;
            var ratio = plot.IsPlanted ? Math.Max(0.18f, (float)plot.Growth / plot.Crop!.GrowthRequired) : 0.18f;
            view.Crop.Scale = new Vector3(1, ratio, 1);
            view.Label.Text = !plot.IsPlanted ? "FARM · EMPTY" : plot.IsMature ? "FARM · HARVEST READY" : $"FARM · GROWING {plot.Growth}/{plot.Crop!.GrowthRequired}";
            view.Label.Modulate = plot.IsMature ? new Color(0.55f, 1, 0.38f) : new Color(1, 0.93f, 0.62f);
        }
    }

    private FarmPresentation CreateFarmPresentation(string name)
    {
        var root = new Node3D { Name = name };
        AddChild(root);
        var soil = new MeshInstance3D
        {
            Name = "Soil", Mesh = new BoxMesh { Size = new Vector3(2.6f, 0.18f, 2.6f) }, Position = new Vector3(0, 0.09f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.25f, 0.12f, 0.055f), Roughness = 1 }
        };
        root.AddChild(soil);
        var borderMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.2f, 0.08f), Roughness = 0.9f };
        foreach (var (position, size) in new[]
        {
            (new Vector3(0, 0.22f, -1.38f), new Vector3(3, 0.28f, 0.18f)),
            (new Vector3(0, 0.22f, 1.38f), new Vector3(3, 0.28f, 0.18f)),
            (new Vector3(-1.38f, 0.22f, 0), new Vector3(0.18f, 0.28f, 3)),
            (new Vector3(1.38f, 0.22f, 0), new Vector3(0.18f, 0.28f, 3))
        })
            root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = position, MaterialOverride = borderMaterial });
        var crop = new Node3D { Name = "Crop" };
        root.AddChild(crop);
        var cropMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.18f, 0.62f, 0.18f), Roughness = 0.85f };
        foreach (var x in new[] { -0.72f, 0f, 0.72f })
        foreach (var z in new[] { -0.72f, 0f, 0.72f })
            crop.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.09f, Height = 0.9f },
                Position = new Vector3(x, 0.57f, z), MaterialOverride = cropMaterial
            });
        var label = new Label3D
        {
            Text = "FARM · EMPTY", Position = new Vector3(0, 2.1f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 36, OutlineSize = 8, Modulate = new Color(1, 0.93f, 0.62f), NoDepthTest = true
        };
        root.AddChild(label);
        return new(root, soil, crop, label);
    }
}
