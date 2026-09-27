using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main : Node3D
{
    private readonly SimulationClock _clock = new();
    private readonly World _world = new();
    private readonly Dictionary<string, EntityView> _entityViews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _entityButtons = new(StringComparer.Ordinal);
    private readonly List<MeshInstance3D> _resourceMarkers = [];
    private FoodConsumptionService _foodConsumption = null!;
    private WoodHaulingService _woodHauling = null!;
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
    private string? _selectedEntityId;

    public override void _Ready()
    {
        _map = GetNode<MeshInstance3D>("Map");
        _obstacle = GetNode<MeshInstance3D>("Obstacle");
        _navigationRegion = GetNode<NavigationRegion3D>("NavigationRegion3D");
        _statusLabel = GetNode<Label>("Controls/Panel/Margin/VBox/Status");
        _pauseButton = GetNode<Button>("Controls/Panel/Margin/VBox/Pause");
        _entityList = GetNode<VBoxContainer>("Controls/Panel/Margin/VBox/EntityList");
        _inspectorLabel = GetNode<Label>("Controls/Panel/Margin/VBox/Inspector");
        _removeSelectedButton = GetNode<Button>("Controls/Panel/Margin/VBox/RemoveSelected");
        _speciesCatalog = SpeciesCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        _foodConsumption = new FoodConsumptionService(_world, GetNavigationAdapter);
        _woodHauling = new WoodHaulingService(_world, GetNavigationAdapter);
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
        _clock.Advance(delta, AdvanceSimulationTick);
        SyncEntityViews();
        UpdateStatus();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
        {
            return;
        }

        if (_selectedEntityId is not { } entityId
            || !_entityViews.TryGetValue(entityId, out var view))
        {
            return;
        }

        switch (key.Keycode)
        {
            case Key.R:
                view.RequestNavigation(new System.Numerics.Vector3(8, 0, 0));
                break;
            case Key.U:
                view.RequestNavigation(new System.Numerics.Vector3(25, 0, 0));
                break;
            case Key.C:
                view.CancelNavigation();
                break;
            case Key.D:
                view.RemoveNavigationTarget();
                break;
            default:
                return;
        }

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
        _woodHauling.Reset();
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

        _selectedEntityId = null;
        UpdateStatus();
    }

    private void AddEntityView(EntityRecord entity)
    {
        var movementSpeed = (float)_speciesCatalog.GetRequired(entity.SpeciesId).MovementSpeed;
        var view = new EntityView { Name = $"View_{entity.Id}" };
        view.Bind(entity.Id, movementSpeed);
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
                _speciesCatalog.GetRequired(entity.SpeciesId));
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
            entity.Needs.Advance(simulationSeconds);
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
            "Select an entity; R route, U unreachable, C cancel, D remove goal";
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
