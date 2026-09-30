using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main : Node3D
{
    private readonly SimulationClock _clock = new();
    private readonly SaveSlotStore _saveSlotStore = new();
    private readonly World _world = new();
    private static readonly System.Numerics.Vector3[] CreatureWanderGoals =
    [
        new(-7f, 0, -2f),
        new(-3f, 0, 6f),
        new(4f, 0, -6f),
        new(7f, 0, 3f)
    ];
    private readonly Dictionary<string, EntityView> _entityViews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _entityButtons = new(StringComparer.Ordinal);
    private readonly List<MeshInstance3D> _resourceMarkers = [];
    private FoodConsumptionService _foodConsumption = null!;
    private WanderingService _wandering = null!;
    private WoodHaulingService _woodHauling = null!;
    private WanderService _wander = null!;
    private long _simulationTick;
    private SpeciesCatalog _speciesCatalog = null!;
    private MeshInstance3D _map = null!;
    private MeshInstance3D _obstacle = null!;
    private NavigationRegion3D _navigationRegion = null!;
    private Label _statusLabel = null!;
    private Button _pauseButton = null!;
    private VBoxContainer _entityList = null!;
    private Label _inspectorLabel = null!;
    private Button _removeSelectedButton = null!;
    private string _saveLoadStatus = "No save loaded.";
    private string _saveSlotPath = null!;
    private string? _selectedEntityId;
    private CanvasLayer _controls = null!;
    private Control _controlsPanel = null!;
    private string? _activeMenu;

    public override void _Ready()
    {
        _map = GetNode<MeshInstance3D>("Map");
        _obstacle = GetNode<MeshInstance3D>("Obstacle");
        _navigationRegion = GetNode<NavigationRegion3D>("NavigationRegion3D");
        _controls = GetNode<CanvasLayer>("Controls");
        _controlsPanel = GetNode<Control>("Controls/Panel");
        _statusLabel = GetNode<Label>("Controls/Panel/Scroll/Margin/VBox/Status");
        _pauseButton = GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/Pause");
        _entityList = GetNode<VBoxContainer>("Controls/Panel/Scroll/Margin/VBox/EntityList");
        _inspectorLabel = GetNode<Label>("Controls/Panel/Scroll/Margin/VBox/Inspector");
        _removeSelectedButton = GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/RemoveSelected");
        _saveSlotPath = ProjectSettings.GlobalizePath("user://ant-farm-save.json");
        _speciesCatalog = SpeciesCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        _foodConsumption = new FoodConsumptionService(_world, GetNavigationAdapter);
        _wandering = new WanderingService(
            _world,
            GetNavigationAdapter,
            [
                new System.Numerics.Vector3(-5, 0, -3),
                new System.Numerics.Vector3(4, 0, 3),
                new System.Numerics.Vector3(7, 0, -5)
            ]);
        _woodHauling = new WoodHaulingService(_world, GetNavigationAdapter);
        _wander = new WanderService(_world, GetNavigationAdapter);
        _pauseButton.Pressed += TogglePause;
        GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/OneX").Pressed += () => _clock.SetSpeed(SimulationSpeed.OneX);
        GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/FourX").Pressed += () => _clock.SetSpeed(SimulationSpeed.FourX);
        GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/Reset").Pressed += ResetScenario;
        GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/Save").Pressed += SaveScenario;
        GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/Load").Pressed += LoadScenario;
        _removeSelectedButton.Pressed += RemoveSelectedEntity;
        ResetScenario();
        UpdateStatus();
    }

    public override void _Process(double delta)
    {
        _clock.Advance(delta, AdvanceSimulationTick);
        SyncEntityViews();
        UpdateStatus();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && (!key.Pressed || key.Echo))
        {
            return;
        }

        // UI clicks belong to the menu. Only left clicks outside it become combat actions.
        if (@event is InputEventMouseButton mouse
            && mouse.ButtonIndex == MouseButton.Left
            && mouse.Pressed
            && _controls.Visible
            && _controlsPanel.GetGlobalRect().HasPoint(mouse.Position))
        {
            return;
        }

        if (Input.IsActionPressed(ControllerActions.CloseMenu))
        {
            _activeMenu = null;
            _controls.Visible = false;
            UpdateStatus();
            return;
        }

        if (Input.IsActionPressed(ControllerActions.Inventory))
        {
            ToggleMenu("Inventory");
            return;
        }

        if (Input.IsActionPressed(ControllerActions.Settlement))
        {
            ToggleMenu("Settlement");
            return;
        }

        if (Input.IsActionPressed(ControllerActions.Collect))
        {
            _activeMenu = "Collection";
            _controls.Visible = true;
            UpdateStatus();
            return;
        }

        if (Input.IsActionPressed(ControllerActions.Attack))
        {
            _activeMenu = "Combat";
            _controls.Visible = true;
            UpdateStatus();
            return;
        }

        if (_selectedEntityId is not { } entityId
            || !_entityViews.TryGetValue(entityId, out var view))
        {
            return;
        }

        if (Input.IsActionPressed(ControllerActions.DebugRoute))
        {
            view.RequestNavigation(new System.Numerics.Vector3(8, 0, 0));
        }
        else if (Input.IsActionPressed(ControllerActions.DebugUnreachable))
        {
            view.RequestNavigation(new System.Numerics.Vector3(25, 0, 0));
        }
        else if (Input.IsActionPressed(ControllerActions.DebugCancel))
        {
            view.CancelNavigation();
        }
        else if (Input.IsActionPressed(ControllerActions.DebugRemoveGoal))
        {
            view.RemoveNavigationTarget();
        }
        else
        {
            return;
        }

        UpdateStatus();
    }

    private void ToggleMenu(string menuName)
    {
        _activeMenu = string.Equals(_activeMenu, menuName, StringComparison.Ordinal)
            ? null
            : menuName;
        _controls.Visible = true;
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
        _foodConsumption.Reset();
        _wandering.Reset();
        _woodHauling.Reset();
        _wander.Reset();
        _simulationTick = 0;
        _world.Reset();
        ClearEntityViews();
        ClearResourceMarkers();
        if (_map is not null)
        {
            _map.Mesh = new PlaneMesh { Size = new Vector2(20, 20) };
        }

        _obstacle.Mesh = new BoxMesh { Size = new Vector3(2, 1, 2) };
        _obstacle.Position = new Vector3(0, 0.5f, 0);
        _navigationRegion.NavigationMesh = CreateNavigationMesh();

        var foodContents = new ResourceContainer("food-pile-1", 100);
        if (foodContents.TryAdd(ItemIds.Food, 100) != ResourceMutationResult.Success)
        {
            throw new InvalidOperationException("Unable to seed the initial food pile.");
        }

        _world.AddFoodSource(
            "food-pile-1",
            new System.Numerics.Vector3(-8, 0, -8),
            foodContents);

        var woodContents = new ResourceContainer("wood-pile-1", 100);
        if (woodContents.TryAdd(ItemIds.Wood, 100) != ResourceMutationResult.Success)
        {
            throw new InvalidOperationException("Unable to seed the initial wood pile.");
        }

        var woodPile = _world.AddResourcePile(
            "wood-pile-1",
            new System.Numerics.Vector3(8, 0, -8),
            woodContents);
        var stockpile = _world.AddStockpile(
            "stockpile-1",
            new System.Numerics.Vector3(-8, 0, 8),
            new ResourceContainer("stockpile-1", 100));
        AddResourceMarker("WoodPile", woodPile.Position, new Color(0.45f, 0.25f, 0.1f));
        AddResourceMarker("Stockpile", stockpile.Position, new Color(0.2f, 0.45f, 0.25f));

        for (var index = 0; index < 10; index++)
        {
            var entity = _world.SpawnEntity("human", new System.Numerics.Vector3(index - 4.5f, 0, 0));
            AddEntityView(entity);
        }

        for (var index = 0; index < 3; index++)
        {
            var entity = _world.SpawnEntity("creature", new System.Numerics.Vector3(index - 1, 0, 4));
            AddEntityView(entity);
        }

        _selectedEntityId = null;
        _saveLoadStatus = "Scenario reset; save slot unchanged.";
        UpdateStatus();
    }

    private void AddEntityView(EntityRecord entity)
    {
        var movementSpeed = (float)_speciesCatalog.GetRequired(entity.SpeciesId).MovementSpeed;
        var view = new EntityView { Name = $"View_{entity.Id}" };
        view.Bind(entity.Id, movementSpeed, isCreature: !string.Equals(entity.SpeciesId, "human", StringComparison.Ordinal));
        AddChild(view);
        _entityViews.Add(entity.Id, view);

        var button = new Button { Name = $"Entity_{entity.Id}", Text = entity.Id };
        button.Pressed += () => SelectEntity(entity.Id);
        _entityList.AddChild(button);
        _entityButtons.Add(entity.Id, button);
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

    private void AdvanceWoodHauling()
    {
        foreach (var entity in _world.Entities)
        {
            _woodHauling.Advance(
                entity,
                _speciesCatalog.GetRequired(entity.SpeciesId),
                _simulationTick);
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
        view?.CancelNavigation();
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

    private void AdvanceSimulationTick(double tickSeconds)
    {
        _simulationTick = checked(_simulationTick + 1);
        AdvanceNeeds(tickSeconds);
        AdvanceNavigationTick(tickSeconds);
        AdvanceFoodConsumption();
        AdvanceWoodHauling();
        AdvanceWandering();
    }

    private void AdvanceNavigationTick(double tickSeconds)
    {
        foreach (var view in _entityViews.Values)
        {
            view.AdvanceNavigation(tickSeconds, _world);
        }
    }

    private void AdvanceNeeds(double simulationSeconds)
    {
        foreach (var entity in _world.Entities)
        {
            entity.Needs.Advance(
                simulationSeconds,
                _speciesCatalog.GetRequired(entity.SpeciesId).HungerRate);
        }
    }

    private void AdvanceFoodConsumption()
    {
        foreach (var entity in _world.Entities)
        {
            _foodConsumption.Advance(
                entity,
                _speciesCatalog.GetRequired(entity.SpeciesId),
                _simulationTick);
        }
    }

    private void AdvanceWandering()
    {
        foreach (var entity in _world.Entities)
        {
            _wandering.Advance(
                entity,
                _speciesCatalog.GetRequired(entity.SpeciesId));
            var species = _speciesCatalog.GetRequired(entity.SpeciesId);
            if (species.CanHaul)
            {
                continue;
            }

            _wander.Advance(entity, species, CreatureWanderGoals);
        }
    }

    private INavigationAdapter GetNavigationAdapter(EntityRecord entity)
    {
        return _entityViews.TryGetValue(entity.Id, out var view)
            ? view.NavigationAdapter
            : throw new InvalidOperationException($"Entity view '{entity.Id}' has no navigation adapter.");
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

    private void ClearResourceMarkers()
    {
        foreach (var marker in _resourceMarkers)
        {
            marker.QueueFree();
        }

        _resourceMarkers.Clear();
    }

    private void AddResourceMarker(string name, System.Numerics.Vector3 position, Color color)
    {
        var marker = new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh { Size = new Vector3(1, 0.6f, 1) },
            Position = new Vector3(position.X, 0.3f, position.Z),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color }
        };
        AddChild(marker);
        _resourceMarkers.Add(marker);
    }

    private void UpdateStatus()
    {
        if (_statusLabel is null)
        {
            return;
        }

        var state = _clock.IsPaused ? "Paused" : "Running";
        var groundWood = _world.ResourcePiles.Sum(
            target => (long)target.Contents.Counts.GetValueOrDefault(ItemIds.Wood));
        var carriedWood = _world.Entities.Sum(
            entity => (long)entity.Inventory.GetValueOrDefault(ItemIds.Wood));
        var storedWood = _world.Stockpiles.Sum(
            target => (long)target.Contents.Counts.GetValueOrDefault(ItemIds.Wood));
        var totalWood = groundWood + carriedWood + storedWood;
        _statusLabel.Text =
            $"Simulation time: {_clock.SimulationTime:0.0}s\nState: {state}\nSpeed: {(int)_clock.Speed}x\n" +
            $"Food: {_foodConsumption.FoodUnitsRemaining} remaining, {_foodConsumption.FoodUnitsConsumed} consumed\n" +
            $"Wood: ground {groundWood}, carried {carriedWood}, stored {storedWood} (total {totalWood})\n" +
            $"Save/load: {_saveLoadStatus}\n" +

            $"Menu: {_activeMenu ?? "closed"} | WASD move, Space jump, LMB combat, E collect, I inventory, Tab settlement, Esc close\n" +
            "Select an entity; F5 route, F6 unreachable, F7 cancel, F8 remove goal";
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
        _entityViews.TryGetValue(entity.Id, out var view);
        var target = view?.NavigationTarget is { } navigationTarget
            ? $"({navigationTarget.X:0.0}, {navigationTarget.Y:0.0}, {navigationTarget.Z:0.0})"
            : "none";
        var navigationStatus = view?.NavigationOutcome?.ToString() ?? "Idle";
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
            $"Target: {target}\n" +
            $"Navigation: {navigationStatus}\n" +
            $"Inventory: {inventory}\n" +
            $"Waiting reason: {entity.WaitingReason ?? "none"}";
        _removeSelectedButton.Disabled = false;
    }

    private void SaveScenario()
    {
        _clock.SetPaused(true);
        try
        {
            _saveSlotStore.Save(
                _saveSlotPath,
                () => WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime));
            _saveLoadStatus = $"Saved at {_clock.SimulationTime:0.0}s.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _saveLoadStatus = $"Save failed: {exception.Message}";
        }

        UpdateStatus();
    }

    private void LoadScenario()
    {
        _clock.SetPaused(true);
        try
        {
            var json = _saveSlotStore.Load(_saveSlotPath);
            var restoredTime = WorldSnapshotSerializer.Load(
                _world,
                json,
                _speciesCatalog,
                _foodConsumption,
                _wandering,
                _woodHauling,
                _wander);
            _clock.Restore(restoredTime);
            _simulationTick = checked((long)Math.Round(
                restoredTime / SimulationClock.TickIntervalSeconds,
                MidpointRounding.AwayFromZero));
            RebuildPresentationFromWorld();
            _saveLoadStatus = $"Loaded {_clock.SimulationTime:0.0}s; paused for inspection.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _saveLoadStatus = $"Load failed: {exception.Message}";
        }

        UpdateStatus();
    }

    private void RebuildPresentationFromWorld()
    {
        ClearEntityViews();
        ClearResourceMarkers();
        foreach (var source in _world.FoodSources)
        {
            AddResourceMarker($"Food_{source.Id}", source.Position, new Color(0.75f, 0.6f, 0.15f));
        }

        foreach (var source in _world.ResourcePiles)
        {
            AddResourceMarker($"Resource_{source.Id}", source.Position, new Color(0.45f, 0.25f, 0.1f));
        }

        foreach (var stockpile in _world.Stockpiles)
        {
            AddResourceMarker($"Stockpile_{stockpile.Id}", stockpile.Position, new Color(0.2f, 0.45f, 0.25f));
        }

        foreach (var entity in _world.Entities)
        {
            AddEntityView(entity);
        }
    }



    private static NavigationMesh CreateNavigationMesh()
    {
        var mesh = new NavigationMesh();
        var vertices = new List<Vector3>();
        var vertexIndices = new Dictionary<(int X, int Z), int>();
        var polygons = new List<int[]>();

        int GetVertex(int x, int z)
        {
            var coordinate = (x, z);
            if (vertexIndices.TryGetValue(coordinate, out var existingIndex))
            {
                return existingIndex;
            }

            var index = vertices.Count;
            vertices.Add(new Vector3(x, 0, z));
            vertexIndices.Add(coordinate, index);
            return index;
        }

        void AddRectangle(int minX, int maxX, int minZ, int maxZ)
        {
            polygons.Add(
            [
                GetVertex(minX, minZ),
                GetVertex(minX, maxZ),
                GetVertex(maxX, maxZ),
                GetVertex(maxX, minZ)
            ]);
        }

        AddRectangle(-10, -1, -10, -1);
        AddRectangle(-1, 1, -10, -1);
        AddRectangle(1, 10, -10, -1);
        AddRectangle(-10, -1, -1, 1);
        AddRectangle(1, 10, -1, 1);
        AddRectangle(-10, -1, 1, 10);
        AddRectangle(-1, 1, 1, 10);
        AddRectangle(1, 10, 1, 10);

        mesh.SetVertices(vertices.ToArray());
        foreach (var polygon in polygons)
        {
            mesh.AddPolygon(polygon);
        }

        return mesh;
    }
}
