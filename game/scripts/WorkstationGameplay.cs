using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private Control _workstationPanel = null!;
    private Label _workstationSummary = null!;
    private Label _workstationMessage = null!;
    private string? _openWorkstationId;

    private bool WorkstationPanelOpen => _workstationPanel is { Visible: true };

    private void ConfigureWorkstationGameplay()
    {
        var content = CreateMenuPanel("Workbench", out _workstationPanel);
        _workstationPanel.AnchorLeft = 0.18f;
        _workstationPanel.AnchorRight = 0.82f;
        _workstationPanel.AnchorBottom = 0.72f;
        _workstationSummary = MenuLabel();
        content.AddChild(_workstationSummary);
        _workstationMessage = MenuLabel("Select an action.");
        _workstationMessage.AddThemeColorOverride("font_color", new Color(1, 0.9f, 0.55f));
        content.AddChild(_workstationMessage);
        var actions = new GridContainer { Columns = 2 };
        content.AddChild(actions);
        AddMenuButton(actions, "Start basic meal", () => RunWorkstationAction(service => service.StartBasicMeal(_openWorkstationId!)));
        AddMenuButton(actions, "Load 1 plant", () => RunWorkstationAction(service => service.LoadOne(_openWorkstationId!, ItemIds.Plant)));
        AddMenuButton(actions, "Load 1 wood", () => RunWorkstationAction(service => service.LoadOne(_openWorkstationId!, ItemIds.Wood)));
        AddMenuButton(actions, "Apply 1 work", () => RunWorkstationAction(service => service.ApplyWork(_openWorkstationId!)));
        AddMenuButton(actions, "Collect output", () => RunWorkstationAction(service => service.CollectOutput(_openWorkstationId!)));
        _workstationPanel.Visible = false;
    }

    private void OpenWorkstation(string id)
    {
        var station = _world.Workstations.FirstOrDefault(item => item.Id == id);
        if (station is null) { ShowActionFeedback("The workbench is unavailable."); return; }
        var player = _world.Entities.FirstOrDefault(entity => entity.Id == _world.PlayerEntityId);
        if (player is null || System.Numerics.Vector3.Distance(player.Position, station.Position) > PlayerWorkstationService.InteractionRange)
        { ShowActionFeedback("Move within 2 m of the workbench."); return; }
        CloseFarm();
        _openWorkstationId = id;
        _workstationMessage.Text = "Use carried materials to produce food.";
        _workstationPanel.Visible = true;
        _playerCharacter.SetGameplayInputEnabled(false);
        UpdateWorkstationPanel();
    }

    private void CloseWorkstation()
    {
        if (_workstationPanel is null) return;
        _workstationPanel.Visible = false;
        _openWorkstationId = null;
        _playerCharacter.SetGameplayInputEnabled(_sessionActive && _menuState.ActiveMenu is null && !FarmPanelOpen && !_clock.IsPaused);
        UpdateWorldSelection();
    }

    private void RunWorkstationAction(Func<PlayerWorkstationService, WorkstationAction> action)
    {
        SyncPlayerPosition();
        var result = action(new PlayerWorkstationService(_world));
        _workstationMessage.Text = result.Message;
        _lastActionMessage = result.Message;
        UpdateWorkstationPanel();
        UpdatePlayerMenuContents();
        UpdateWorldSelection();
    }

    private void UpdateWorkstationPanel()
    {
        if (!WorkstationPanelOpen || _openWorkstationId is null) return;
        var station = _world.Workstations.FirstOrDefault(item => item.Id == _openWorkstationId);
        if (station is null) { CloseWorkstation(); return; }
        var recipe = station.CurrentRecipe;
        var recipeText = recipe is null ? "No active recipe" : $"{recipe.Id}: work {recipe.WorkProgress}/{recipe.WorkRequired}";
        _workstationSummary.Text = $"{recipeText}\nRequired: 1 plant + 1 wood → 2 food\nInput: {ContainerSummary(station.InputStorage)}\nOutput: {ContainerSummary(station.OutputStorage)}";
    }
}
