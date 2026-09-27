using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main : Node3D
{
    private readonly SimulationClock _clock = new();
    private readonly World _world = new();
    private readonly Dictionary<string, EntityView> _entityViews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _entityButtons = new(StringComparer.Ordinal);
    private MeshInstance3D _map = null!;
    private Label _statusLabel = null!;
    private Button _pauseButton = null!;
    private VBoxContainer _entityList = null!;
    private Label _inspectorLabel = null!;
    private Button _removeSelectedButton = null!;
    private string? _selectedEntityId;

    public override void _Ready()
    {
        _map = GetNode<MeshInstance3D>("Map");
        _statusLabel = GetNode<Label>("Controls/Panel/Margin/VBox/Status");
        _pauseButton = GetNode<Button>("Controls/Panel/Margin/VBox/Pause");
        _entityList = GetNode<VBoxContainer>("Controls/Panel/Margin/VBox/EntityList");
        _inspectorLabel = GetNode<Label>("Controls/Panel/Margin/VBox/Inspector");
        _removeSelectedButton = GetNode<Button>("Controls/Panel/Margin/VBox/RemoveSelected");
        _pauseButton.Pressed += TogglePause;
        GetNode<Button>("Controls/Panel/Margin/VBox/OneX").Pressed += () => _clock.SetSpeed(SimulationSpeed.OneX);
        GetNode<Button>("Controls/Panel/Margin/VBox/FourX").Pressed += () => _clock.SetSpeed(SimulationSpeed.FourX);
        GetNode<Button>("Controls/Panel/Margin/VBox/Reset").Pressed += ResetScenario;
        _removeSelectedButton.Pressed += RemoveSelectedEntity;
        ResetScenario();
        UpdateStatus();
    }

    public override void _Process(double delta)
    {
        _clock.Advance(delta, AdvanceNeeds);
        SyncEntityViews();
        UpdateStatus();
    }

    private void TogglePause()
    {
        _clock.SetPaused(!_clock.IsPaused);
        UpdateStatus();
    }

    private void ResetScenario()
    {
        _clock.Reset();
        _world.Reset();
        ClearEntityViews();
        if (_map is not null)
        {
            _map.Mesh = new PlaneMesh { Size = new Vector2(20, 20) };
        }

        for (var index = 0; index < 10; index++)
        {
            var entity = _world.SpawnEntity("human", new System.Numerics.Vector3(index - 4.5f, 0, 0));
            AddEntityView(entity.Id);
        }

        _selectedEntityId = null;
        UpdateStatus();
    }

    private void AddEntityView(string entityId)
    {
        var view = new EntityView { Name = $"View_{entityId}" };
        view.Bind(entityId);
        AddChild(view);
        _entityViews.Add(entityId, view);

        var button = new Button { Name = $"Entity_{entityId}", Text = entityId };
        button.Pressed += () => SelectEntity(entityId);
        _entityList.AddChild(button);
        _entityButtons.Add(entityId, button);
        view.Sync(_world);
    }

    private void SelectEntity(string entityId)
    {
        if (_world.TryGetEntity(entityId, out _))
        {
            _selectedEntityId = entityId;
            UpdateInspector();
        }
    }

    private void RemoveSelectedEntity()
    {
        if (_selectedEntityId is not { } entityId)
        {
            return;
        }

        if (!_world.TryRemoveEntity(entityId))
        {
            _selectedEntityId = null;
            UpdateInspector();
            return;
        }

        _entityViews.Remove(entityId, out var view);
        view?.QueueFree();
        _entityButtons.Remove(entityId, out var button);
        button?.QueueFree();
        _selectedEntityId = null;
        UpdateInspector();
    }

    private void SyncEntityViews()
    {
        foreach (var view in _entityViews.Values)
        {
            view.Sync(_world);
        }
    }

    private void AdvanceNeeds(double simulationSeconds)
    {
        foreach (var entity in _world.Entities)
        {
            entity.Needs.Advance(simulationSeconds);
        }
    }

    private void ClearEntityViews()
    {
        foreach (var view in _entityViews.Values)
        {
            view.QueueFree();
        }

        foreach (var button in _entityButtons.Values)
        {
            button.QueueFree();
        }

        _entityViews.Clear();
        _entityButtons.Clear();
        _selectedEntityId = null;
    }

    private void UpdateStatus()
    {
        if (_statusLabel is null)
        {
            return;
        }

        var state = _clock.IsPaused ? "Paused" : "Running";
        _statusLabel.Text = $"Simulation time: {_clock.SimulationTime:0.0}s\nState: {state}\nSpeed: {(int)_clock.Speed}x";
        _pauseButton.Text = _clock.IsPaused ? "Resume" : "Pause";
        UpdateInspector();
    }

    private void UpdateInspector()
    {
        if (_inspectorLabel is null)
        {
            return;
        }

        if (_selectedEntityId is not { } entityId
            || !_world.TryGetEntity(entityId, out var entity)
            || entity is null)
        {
            _inspectorLabel.Text = "Select an entity to inspect.";
            _removeSelectedButton.Disabled = true;
            return;
        }

        var inventory = entity.Inventory.Count == 0
            ? "empty"
            : string.Join(", ", entity.Inventory.Select(item => $"{item.Key}: {item.Value}"));
        var needState = entity.Needs.IsStarving
            ? "Starving"
            : entity.Needs.IsHungry
                ? "Hungry (food requested)"
                : "Satisfied";
        _inspectorLabel.Text =
            $"ID: {entity.Id}\n" +
            $"Species: {entity.SpeciesId}\n" +
            $"Position: {entity.Position}\n" +
            $"Satiety: {entity.Satiety:0.0}\n" +
            $"Needs: {needState}\n" +
            $"Activity: {entity.Activity}\n" +
            "Target: none\n" +
            $"Inventory: {inventory}\n" +
            "Waiting reason: none";
        _removeSelectedButton.Disabled = false;
    }
}
