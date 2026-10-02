using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main : Node3D
{
    private readonly SimulationClock _clock = new();
    private readonly SaveSlotStore _saveSlotStore = new();
    private World _world = new();
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
    private FoodConsumptionService? _foodConsumption;
    private WanderingService? _wandering;
    private WoodHaulingService? _woodHauling;
    private WanderService? _wander;
    private long _simulationTick;
    private SpeciesCatalog _speciesCatalog = null!;
    private RelationshipEventCatalog _relationshipEventCatalog = null!;
    private PlayerCommandBoundary? _playerCommands;
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
    private PlayerCharacter _playerCharacter = null!;
    private CanvasLayer _characterCreation = null!;
    private LineEdit _playerNameInput = null!;
    private OptionButton _heightChoice = null!;
    private OptionButton _bodyTypeChoice = null!;
    private OptionButton _sexChoice = null!;
    private Label _creationError = null!;
    private PlayerCharacterProfile? _playerProfile;
    private int _sessionSeed = NewGameSession.DefaultSeed;
    private bool _sessionActive;
    private string? _activeMenu;
    private string _lastActionMessage = "No action requested.";

    public override void _Ready()
    {
        _map = GetNode<MeshInstance3D>("Map");
        _obstacle = GetNode<MeshInstance3D>("Obstacle");
        _navigationRegion = GetNode<NavigationRegion3D>("NavigationRegion3D");
        _controls = GetNode<CanvasLayer>("Controls");
        _controlsPanel = GetNode<Control>("Controls/Panel");
        _playerCharacter = GetNode<PlayerCharacter>("PlayerCharacter");
        _characterCreation = GetNode<CanvasLayer>("CharacterCreation");
        _playerNameInput = GetNode<LineEdit>("CharacterCreation/Panel/Form/Name");
        _heightChoice = GetNode<OptionButton>("CharacterCreation/Panel/Form/Height");
        _bodyTypeChoice = GetNode<OptionButton>("CharacterCreation/Panel/Form/BodyType");
        _sexChoice = GetNode<OptionButton>("CharacterCreation/Panel/Form/Sex");
        _creationError = GetNode<Label>("CharacterCreation/Panel/Form/Error");
        _statusLabel = GetNode<Label>("Controls/Panel/Scroll/Margin/VBox/Status");
        _pauseButton = GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/Pause");
        _entityList = GetNode<VBoxContainer>("Controls/Panel/Scroll/Margin/VBox/EntityList");
        _inspectorLabel = GetNode<Label>("Controls/Panel/Scroll/Margin/VBox/Inspector");
        _removeSelectedButton = GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/RemoveSelected");
        _saveSlotPath = ProjectSettings.GlobalizePath("user://ant-farm-save.json");
        _speciesCatalog = SpeciesCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        _relationshipEventCatalog = RelationshipEventCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "relationship_events.json"));
        ConfigureCreationChoices();
        _pauseButton.Pressed += TogglePause;
        GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/OneX").Pressed += () => _clock.SetSpeed(SimulationSpeed.OneX);
        GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/FourX").Pressed += () => _clock.SetSpeed(SimulationSpeed.FourX);
        GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/Reset").Pressed += ResetScenario;
        GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/Save").Pressed += SaveScenario;
        GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/Load").Pressed += LoadScenario;
        _removeSelectedButton.Pressed += RemoveSelectedEntity;
        GetNode<Button>("CharacterCreation/Panel/Form/Start").Pressed += StartNewGame;
        _controls.Visible = false;
        _playerCharacter.Visible = false;
        _playerCharacter.SetPhysicsProcess(false);
        _playerCharacter.SetProcessUnhandledInput(false);
        _characterCreation.Visible = true;
        _playerNameInput.GrabFocus();
    }

    public override void _Process(double delta)
    {
        if (!_sessionActive)
        {
            return;
        }

        _clock.Advance(delta, AdvanceSimulationTick);
        SyncPlayerPosition();
        SyncEntityViews();
        UpdateStatus();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_sessionActive)
        {
            return;
        }

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
            // Combat is an immediate world action; it must not open a menu.
            _lastActionMessage = _selectedEntityId is null
                ? "Combat requested; select an entity first."
                : $"Combat requested for {_selectedEntityId}.";
            UpdateStatus();
            return;
        }

        if (Input.IsActionJustPressed(ControllerActions.Interact))
        {
            SyncPlayerPosition();
            var result = _playerCommands?.Execute(
                new PlayerCommand(PlayerCommandKind.Interact, _selectedEntityId));
            _lastActionMessage = result?.Reason
                ?? "Friendly interaction is not initialized.";
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
        _playerCharacter.SetPhysicsProcess(!_clock.IsPaused);
        UpdateStatus();
    }

    private void ResetScenario()
    {
        if (_playerProfile is not { } profile)
        {
            return;
        }

        StartSession(profile, _sessionSeed, "Scenario reset; save slot unchanged.");
    }

    private void ConfigureCreationChoices()
    {
        foreach (var value in Enum.GetNames<PlayerHeight>())
        {
            _heightChoice.AddItem(value);
        }

        foreach (var value in Enum.GetNames<PlayerBodyType>())
        {
            _bodyTypeChoice.AddItem(value);
        }

        foreach (var value in Enum.GetNames<PlayerSex>())
        {
            _sexChoice.AddItem(value);
        }

        _heightChoice.Select((int)PlayerHeight.Medium);
        _bodyTypeChoice.Select((int)PlayerBodyType.Average);
        _sexChoice.Select((int)PlayerSex.Female);
    }

    private void StartNewGame()
    {
        try
        {
            var profile = PlayerCharacterProfile.Create(
                _playerNameInput.Text,
                (PlayerHeight)_heightChoice.Selected,
                (PlayerBodyType)_bodyTypeChoice.Selected,
                (PlayerSex)_sexChoice.Selected);
            StartSession(profile, NewGameSession.DefaultSeed, "New game started.");
            _creationError.Text = string.Empty;
        }
        catch (ArgumentException exception)
        {
            _creationError.Text = exception.Message;
        }
    }

    private void StartSession(
        PlayerCharacterProfile profile,
        int seed,
        string saveLoadMessage)
    {
        var session = NewGameSession.Create(_speciesCatalog, profile, seed);
        _sessionActive = false;
        _playerCharacter.SetPhysicsProcess(false);
        ResetTransientServices();
        _clock.Reset();
        _simulationTick = 0;
        ClearEntityViews();
        ClearResourceMarkers();
        _world = session.World;
        _playerCommands = new PlayerCommandBoundary(
            _world,
            _clock,
            new CreatureGreetingService(_world, _speciesCatalog, _relationshipEventCatalog));
        _playerProfile = profile;
        _sessionSeed = seed;

        _map.Mesh = new PlaneMesh { Size = new Vector2(20, 20) };
        _obstacle.Mesh = new BoxMesh { Size = new Vector3(2, 1, 2) };
        _obstacle.Position = new Vector3(0, 0.5f, 0);
        _navigationRegion.NavigationMesh = CreateNavigationMesh();

        _foodConsumption = new FoodConsumptionService(_world, GetNavigationAdapter);
        _wandering = new WanderingService(
            _world,
            GetNavigationAdapter,
            [
                new System.Numerics.Vector3(-5, 0, -3),
                new System.Numerics.Vector3(4, 0, 3),
                new System.Numerics.Vector3(7, 0, -5)
            ],
            _sessionSeed);
        _woodHauling = new WoodHaulingService(_world, GetNavigationAdapter);
        _wander = new WanderService(_world, GetNavigationAdapter);

        var woodPile = _world.ResourcePiles.Single();
        var stockpile = _world.Stockpiles.Single();
        AddResourceMarker("WoodPile", woodPile.Position, new Color(0.45f, 0.25f, 0.1f));
        AddResourceMarker("Stockpile", stockpile.Position, new Color(0.2f, 0.45f, 0.25f));

        var player = session.Player;
        _playerCharacter.GlobalPosition = new Vector3(
            player.Position.X,
            player.Position.Y,
            player.Position.Z);
        _playerCharacter.BindPlayer(
            player.Id,
            profile,
            (float)_speciesCatalog.GetRequired(player.SpeciesId).MovementSpeed);
        _playerCharacter.Visible = true;
        _playerCharacter.SetProcessUnhandledInput(true);
        _playerCharacter.SetPhysicsProcess(true);

        foreach (var entity in _world.Entities)
        {
            if (!string.Equals(entity.Id, session.PlayerEntityId, StringComparison.Ordinal))
            {
                AddEntityView(entity);
            }
        }

        _selectedEntityId = null;
        _activeMenu = null;
        _saveLoadStatus = saveLoadMessage;
        _sessionActive = true;
        _characterCreation.Visible = false;
        _controls.Visible = true;
        UpdateStatus();
    }

    private void ResetTransientServices()
    {
        _foodConsumption?.Reset();
        _wandering?.Reset();
        _woodHauling?.Reset();
        _wander?.Reset();
    }

    private void AddEntityView(EntityRecord entity)
    {
        var movementSpeed = (float)_speciesCatalog.GetRequired(entity.SpeciesId).MovementSpeed;
        var view = new EntityView { Name = $"View_{entity.Id}" };
        view.Bind(entity.Id, movementSpeed, isCreature: !string.Equals(entity.SpeciesId, "human", StringComparison.Ordinal), entity.SpeciesId);
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
            if (string.Equals(entity.Id, _world.PlayerEntityId, StringComparison.Ordinal))
            {
                continue;
            }

            _woodHauling!.Advance(
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

    private void SyncPlayerPosition()
    {
        if (_world.PlayerEntityId is not { } playerId)
        {
            return;
        }

        var position = _playerCharacter.GlobalPosition;
        _world.TrySetEntityPosition(
            playerId,
            new System.Numerics.Vector3(position.X, position.Y, position.Z));
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
            if (string.Equals(entity.Id, _world.PlayerEntityId, StringComparison.Ordinal))
            {
                continue;
            }

            _foodConsumption!.Advance(
                entity,
                _speciesCatalog.GetRequired(entity.SpeciesId),
                _simulationTick);
        }
    }

    private void AdvanceWandering()
    {
        foreach (var entity in _world.Entities)
        {
            if (string.Equals(entity.Id, _world.PlayerEntityId, StringComparison.Ordinal))
            {
                continue;
            }

            _wandering!.Advance(
                entity,
                _speciesCatalog.GetRequired(entity.SpeciesId),
                _simulationTick);
            var species = _speciesCatalog.GetRequired(entity.SpeciesId);
            if (species.CanHaul)
            {
                continue;
            }

            _wander!.Advance(entity, species, CreatureWanderGoals);
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
        var foodConsumption = _foodConsumption
            ?? throw new InvalidOperationException("Food simulation is not initialized for the active session.");
        var playerStatus = _world.PlayerProfile is { } profile
            ? $"Player: {profile.Name} ({profile.Height}, {profile.BodyType}, {profile.Sex})\n"
            : string.Empty;
        _statusLabel.Text =
            $"Simulation time: {_clock.SimulationTime:0.0}s\nState: {state}\nSpeed: {(int)_clock.Speed}x\n" +
            playerStatus +
            $"Food: {foodConsumption.FoodUnitsRemaining} remaining, {foodConsumption.FoodUnitsConsumed} consumed\n" +
            $"Wood: ground {groundWood}, carried {carriedWood}, stored {storedWood} (total {totalWood})\n" +
            $"Save/load: {_saveLoadStatus}\n" +

            $"Menu: {_activeMenu ?? "closed"} | WASD move, Space jump, LMB combat, E collect, G greet, I inventory, Tab settlement, Esc close\n" +
            $"Last action: {_lastActionMessage}\n" +
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
        var inspection = EntityInspectionReadModel.Create(_world, entity);
        var relationships = EntityInspectionReadModel.FormatOutgoingRelationships(inspection);
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
            $"Waiting reason: {entity.WaitingReason ?? "none"}\n" +
            $"Outgoing relationships:\n{relationships}";
        _removeSelectedButton.Disabled = false;
    }

    private void SaveScenario()
    {
        _clock.SetPaused(true);
        _playerCharacter.SetPhysicsProcess(false);
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
        _playerCharacter.SetPhysicsProcess(false);
        try
        {
            var json = _saveSlotStore.Load(_saveSlotPath);
            var restoredTime = WorldSnapshotSerializer.Load(
                _world,
                json,
                _speciesCatalog,
                _foodConsumption!,
                _wandering!,
                _woodHauling!,
                _wander!);
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
        _playerProfile = _world.PlayerProfile;
        _sessionSeed = _world.SessionSeed ?? NewGameSession.DefaultSeed;
        if (_world.PlayerEntityId is { } playerId
            && _world.TryGetEntity(playerId, out var player)
            && player is not null
            && _playerProfile is { } profile)
        {
            _playerCharacter.GlobalPosition = new Vector3(
                player.Position.X,
                player.Position.Y,
                player.Position.Z);
            _playerCharacter.BindPlayer(
                player.Id,
                profile,
                (float)_speciesCatalog.GetRequired(player.SpeciesId).MovementSpeed);
            _playerCharacter.Visible = true;
            _playerCharacter.SetProcessUnhandledInput(true);
            _playerCharacter.SetPhysicsProcess(false);
        }
        else
        {
            _playerCharacter.Visible = false;
            _playerCharacter.SetProcessUnhandledInput(false);
        }

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
            if (!string.Equals(entity.Id, _world.PlayerEntityId, StringComparison.Ordinal))
            {
                AddEntityView(entity);
            }
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
