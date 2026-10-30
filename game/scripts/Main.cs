using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main : Node3D
{
	[Export]
	public bool SkipCharacterCreationForTesting { get; set; } = true;

	private readonly SimulationClock _clock = new();
	private readonly SaveSlotStore _saveSlotStore = new();
	private World _world = new();
	private const float WorldHalfExtent = 200f;
	private const float TownHalfExtent = 60f;
	private static readonly float[] NavigationGridLines =
		[-200f, -120f, -60f, -38f, -30f, -22f, -14f, -1f, 1f, 8f, 12f, 14f, 22f, 30f, 38f, 60f, 120f, 200f];
	private static readonly (float Min, float Max)[] BuildingBands =
		[(-38f, -30f), (-22f, -14f), (14f, 22f), (30f, 38f)];
	private Node3D? _scenery;
	private readonly Dictionary<string, EntityView> _entityViews = new(StringComparer.Ordinal);
	private readonly Dictionary<string, Button> _entityButtons = new(StringComparer.Ordinal);
	private readonly Dictionary<WorldTarget, MeshInstance3D> _resourceMarkers = [];
	private readonly Dictionary<string, MeshInstance3D> _fireplaceViews = [];
	private readonly Dictionary<string, MeshInstance3D> _workbenchViews = new(StringComparer.Ordinal);
	private FoodConsumptionService? _foodConsumption;
	private CreatureHuntingService? _creatureHunting;
	private WildPreyThreatService? _wildPreyThreat;
	private WanderingService? _wandering;
	private WoodHaulingService? _woodHauling;
	private SquadOrderService? _squad;
	private OrderService? _orders;
	private SpinBox _squadX = null!;
	private SpinBox _squadZ = null!;
	private long _simulationTick;
	private TreeRegrowthService? _treeRegrowth;
	private CarcassDecayService? _carcassDecay;
	private SpeciesCatalog _speciesCatalog = null!;
	private EntityPresentationCatalog _entityPresentationCatalog = null!;
	private WildlifeRegionCatalog _wildlifeRegionCatalog = null!;
	private WildlifeProximityCoordinator? _wildlifeProximity;
	private RelationshipEventCatalog _relationshipEventCatalog = null!;
	private TradeGoodsCatalog _tradeGoodsCatalog = null!;
	private InteractionResolver _interactionResolver = null!;
	private PlayerCommandBoundary? _playerCommands;
	private MeshInstance3D _map = null!;
	private MeshInstance3D _workbench = null!;
	private NavigationRegion3D _navigationRegion = null!;
	private Label _statusLabel = null!;
	private Button _pauseButton = null!;
	private VBoxContainer _entityList = null!;
	private Label _inspectorLabel = null!;
	private Button _removeSelectedButton = null!;
	private Button _followSelectedOrderButton = null!;
	private Button _staySelectedOrderButton = null!;
	private PopupMenu _entityContextMenu = null!;
	private string? _contextEntityId;
	private string _saveLoadStatus = "No save loaded.";
	private string _saveSlotPath = null!;
	private WorldTarget? _selectedTarget;
	private bool _targetLocked;
	private double _targetLockLostSeconds;
	// Existing entity actions and the Settlement list consume the same selection.
	private string? _selectedEntityId
	{
		get => _selectedTarget is { Kind: TargetKind.Entity } target ? target.Id : null;
		set => _selectedTarget = value is null ? null : new(TargetKind.Entity, value);
	}
	private CanvasLayer _controls = null!;
	private Control _controlsPanel = null!;
	private PlayerCharacter _playerCharacter = null!;
	private CanvasLayer _characterCreation = null!;
	private LineEdit _playerNameInput = null!;
	private OptionButton _heightChoice = null!;
	private OptionButton _bodyTypeChoice = null!;
	private OptionButton _sexChoice = null!;
	private HSlider _weightChoice = null!;
	private HSlider _muscleChoice = null!;
	private Label _weightLabel = null!;
	private Label _muscleLabel = null!;
	private Label _creationPreview = null!;
	private Node3D _previewRoot = null!;
	private double _previewTime;
	private Label _creationError = null!;
	private PlayerCharacterProfile? _playerProfile;
	private int _sessionSeed = NewGameSession.DefaultSeed;
	private bool _survivalGathering;
	private bool _starterWildlifeAsFar;
	private bool _sessionActive;
	private readonly PlayerMenuState _menuState = new();
	private string _lastActionMessage = "No action requested.";

	public override void _Ready()
	{
		_map = GetNode<MeshInstance3D>("Map");
		_workbench = GetNode<MeshInstance3D>("Workbench");
		_navigationRegion = GetNode<NavigationRegion3D>("NavigationRegion3D");
		_controls = GetNode<CanvasLayer>("Controls");
		_controlsPanel = GetNode<Control>("Controls/Panel");
		_playerCharacter = GetNode<PlayerCharacter>("PlayerCharacter");
		_characterCreation = GetNode<CanvasLayer>("CharacterCreation");
		_playerNameInput = GetNode<LineEdit>("CharacterCreation/Panel/Form/Name");
		_heightChoice = GetNode<OptionButton>("CharacterCreation/Panel/Form/Height");
		_bodyTypeChoice = GetNode<OptionButton>("CharacterCreation/Panel/Form/BodyType");
		_sexChoice = GetNode<OptionButton>("CharacterCreation/Panel/Form/Sex");
		_weightChoice = GetNode<HSlider>("CharacterCreation/Panel/Form/Weight");
		_muscleChoice = GetNode<HSlider>("CharacterCreation/Panel/Form/Muscle");
		_weightLabel = GetNode<Label>("CharacterCreation/Panel/Form/WeightLabel");
		_muscleLabel = GetNode<Label>("CharacterCreation/Panel/Form/MuscleLabel");
		_creationPreview = GetNode<Label>("CharacterCreation/Panel/Form/Preview");
		_previewRoot = GetNode<Node3D>("CharacterCreation/Panel/Form/PreviewViewport/SubViewport/PreviewRoot");
		_weightChoice.ValueChanged += value => { _weightLabel.Text = $"Weight: {value * 100:0}%"; UpdateCreationPreview(); };
		_muscleChoice.ValueChanged += value => { _muscleLabel.Text = $"Muscle: {value * 100:0}%"; UpdateCreationPreview(); };
		_heightChoice.ItemSelected += _ => UpdateCreationPreview();
		_bodyTypeChoice.ItemSelected += _ => UpdateCreationPreview();
		_sexChoice.ItemSelected += _ => UpdateCreationPreview();
		_creationError = GetNode<Label>("CharacterCreation/Panel/Form/Error");
		_statusLabel = GetNode<Label>("Controls/Panel/Scroll/Margin/VBox/Status");
		_pauseButton = GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/Pause");
		_entityList = GetNode<VBoxContainer>("Controls/Panel/Scroll/Margin/VBox/EntityList");
		_inspectorLabel = GetNode<Label>("Controls/Panel/Scroll/Margin/VBox/Inspector");
		_statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_inspectorLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_statusLabel.CustomMinimumSize = new Vector2(300, 0);
		_inspectorLabel.CustomMinimumSize = new Vector2(300, 0);
		_removeSelectedButton = GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/RemoveSelected");
		_saveSlotPath = ProjectSettings.GlobalizePath("user://ant-farm-save.json");
		_speciesCatalog = SpeciesCatalog.LoadFromFile(
			Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
		_entityPresentationCatalog = EntityPresentationCatalog.LoadFromResource("res://data/entity_presentations.json");
		_wildlifeRegionCatalog = WildlifeRegionCatalog.LoadFromFile(
			Path.Combine(AppContext.BaseDirectory, "data", "wildlife_regions.json"), _speciesCatalog);
		_relationshipEventCatalog = RelationshipEventCatalog.LoadFromFile(
			Path.Combine(AppContext.BaseDirectory, "data", "relationship_events.json"));
		_tradeGoodsCatalog = TradeGoodsCatalog.LoadFromFile(
			Path.Combine(AppContext.BaseDirectory, "data", "trade_goods.json"));
		ConfigureCreationChoices();
		_pauseButton.Pressed += TogglePause;
		GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/OneX").Pressed += () => _clock.SetSpeed(SimulationSpeed.OneX);
		GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/FourX").Pressed += () => _clock.SetSpeed(SimulationSpeed.FourX);
		GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/Reset").Pressed += ResetScenario;
		GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/Save").Pressed += SaveScenario;
		GetNode<Button>("Controls/Panel/Scroll/Margin/VBox/Load").Pressed += LoadScenario;
		_removeSelectedButton.Pressed += RemoveSelectedEntity;
		ConfigureSquadControls();
		ConfigureScheduleControls();
		ConfigurePlayerMenus();
		ConfigureIndividualOrderControls();
		ConfigureWorkstationGameplay();
		ConfigureFarmGameplay();
		ConfigureConstructionGameplay();
		ConfigureWorldSelection();
		ConfigureGameplayActions();
		ApplyMenuState();
		GetNode<Button>("CharacterCreation/Panel/Form/Start").Pressed += StartNewGame;
		_controls.Visible = false;
		_playerCharacter.Visible = false;
		_playerCharacter.SetPhysicsProcess(false);
		_playerCharacter.SetProcessUnhandledInput(false);
		if (SkipCharacterCreationForTesting)
		{
			_characterCreation.Visible = false;
			StartSession(
				PlayerCharacterProfile.Create("Test Player"),
				NewGameSession.DefaultSeed,
				"Test game started; character creation bypassed.",
				startStarterWildlifeAsFar: true);
		}
		else
		{
			_characterCreation.Visible = true;
			_playerNameInput.GrabFocus();
		}
		if (OS.GetCmdlineUserArgs().Contains("--validate-basic-settlement"))
			Callable.From(RunBasicSettlementValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-construction"))
			Callable.From(RunConstructionValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-farm"))
			Callable.From(RunFarmValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-workbench"))
			Callable.From(RunWorkbenchValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-social-feedback"))
			Callable.From(RunSocialFeedbackValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-follow-spacing"))
			Callable.From(RunFollowSpacingValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-squad"))
			Callable.From(RunSquadValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-individual-orders"))
			Callable.From(RunIndividualOrderValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-first-game"))
			Callable.From(RunFirstGameValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-session-profiles"))
			Callable.From(RunSessionProfileValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-schedules"))
			Callable.From(RunScheduleValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-schedule-integration"))
			Callable.From(RunScheduleValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-menus"))
			Callable.From(RunMenuValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-selection"))
			Callable.From(RunSelectionValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-gameplay-actions"))
			Callable.From(RunGameplayActionsValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-interaction-anchors"))
			Callable.From(RunInteractionAnchorValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-wild-territories"))
			Callable.From(RunWildTerritoryValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-wild-prey-threat"))
			Callable.From(RunWildPreyThreatValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-wolf-hunting"))
			Callable.From(RunWolfHuntingValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-carcass-decay"))
			Callable.From(RunCarcassDecayValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-far-population-transitions"))
			Callable.From(RunFarPopulationTransitionsValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-starter-wildlife-proximity"))
			Callable.From(RunStarterWildlifeProximityValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-npc-locomotion"))
			Callable.From(RunNpcLocomotionValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-species-presentations"))
			Callable.From(RunEntityPresentationValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-player-workwear"))
			Callable.From(RunPlayerWorkwearValidation).CallDeferred();
		else if (OS.GetCmdlineUserArgs().Contains("--validate-character-lighting"))
			Callable.From(RunCharacterLightingValidation).CallDeferred();
	}

	private void RunPlayerWorkwearValidation()
	{
		var validationCharacter = GD.Load<PackedScene>("res://player_character.tscn")
			.Instantiate<PlayerCharacter>();
		validationCharacter.Name = "PlayerWorkwearValidationCharacter";
		validationCharacter.Visible = false;
		validationCharacter.SetPhysicsProcess(false);
		validationCharacter.SetProcessUnhandledInput(false);
		AddChild(validationCharacter);
		PlayerWorkwearValidation.Run(this, validationCharacter, _speciesCatalog);
	}

	private void RunCharacterLightingValidation()
	{
		try
		{
			CharacterLightingValidation.Run(
				GetNode<WorldEnvironment>("WorldEnvironment"),
				GetNode<DirectionalLight3D>("DirectionalLight3D"));
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"CHARACTER LIGHTING VALIDATION FAILED: {exception}");
			GetTree().Quit(1);
		}
	}

	public override void _Process(double delta)
	{
		if (!_sessionActive)
		{
			if (_characterCreation.Visible && _previewRoot is not null)
			{
				_previewTime += Math.Max(0, delta);
				_previewRoot.Rotation = new Vector3(0, (float)(_previewTime * 0.45), 0);
				_previewRoot.Position = new Vector3(0, Mathf.Sin((float)_previewTime * 1.4f) * 0.025f, 0);
			}
			return;
		}

		_clock.Advance(delta, AdvanceSimulationTick);
		AdvanceCollectUse(delta);
		UpdateGameplayFeedback(delta);
		AdvanceTargetLockGrace(delta);
		SyncPlayerState();
		SyncEntityViews();
		UpdateStatus();
		UpdateWorkstationPanel();
		UpdateFarmPanel();
		UpdateFarmPresentation();
		if (_placingHut) UpdateHutPreview();
		else if (_placingFireplace) UpdateFireplacePreview();
		else if (_placingWorkbench) UpdateWorkbenchPreview();
		else if (_placingFarm) UpdateFarmPlacementPreview();
		UpdateConstructionPanel();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_sessionActive || _menuState.ActiveMenu is not null || WorkstationPanelOpen || FarmPanelOpen || ConstructionPanelOpen || PlacementPanelOpen || _clock.IsPaused)
		{
			return;
		}
		if (_world.PlayerEntityId is { } sleepingPlayerId && _world.TryGetEntity(sleepingPlayerId, out var sleepingPlayer)
			&& sleepingPlayer is not null && sleepingPlayer.SleepState == SleepState.Sleeping)
		{
			if (@event.IsActionPressed(ControllerActions.Rest)) PerformRestAction();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event.IsActionReleased(ControllerActions.Collect))
		{
			CompleteCollectUse();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is InputEventKey key && (!key.Pressed || key.Echo))
		{
			return;
		}

		if (@event.IsActionPressed(ControllerActions.CycleTarget))
		{
			CycleWorldTarget();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event.IsActionPressed(ControllerActions.Collect))
		{
			BeginCollectUse();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event.IsActionPressed(ControllerActions.AttackLeft) || @event.IsActionPressed(ControllerActions.AttackRight))
		{
			// PlayerCharacter captures the target only when a swing actually begins.
			return;
		}

		if (@event.IsActionPressed(ControllerActions.Interact))
		{
			PerformFriendlyInteraction();
			return;
		}

		if (@event.IsActionPressed(ControllerActions.Greet))
		{
			PerformGreeting();
			return;
		}

		if (@event.IsActionPressed(ControllerActions.Rest))
		{
			PerformRestAction();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_selectedEntityId is not { } entityId
			|| !_entityViews.TryGetValue(entityId, out var view))
		{
			return;
		}

		if (@event.IsActionPressed(ControllerActions.DebugRoute))
		{
			view.RequestNavigation(new System.Numerics.Vector3(8, 0, 0));
		}
		else if (@event.IsActionPressed(ControllerActions.DebugUnreachable))
		{
			view.RequestNavigation(new System.Numerics.Vector3(25, 0, 0));
		}
		else if (@event.IsActionPressed(ControllerActions.DebugCancel))
		{
			view.CancelNavigation();
		}
		else if (@event.IsActionPressed(ControllerActions.DebugRemoveGoal))
		{
			view.RemoveNavigationTarget();
		}
		else
		{
			return;
		}

		UpdateStatus();
	}

	private void ToggleMenu(PlayerMenu menu)
	{
		_menuState.Toggle(menu);
		ApplyMenuState();
		UpdateFarmPresentation();
		SyncConstructionPresentation();
		UpdateStatus();
	}

	private void TogglePause()
	{
		CancelGameplayActions();
		_clock.SetPaused(!_clock.IsPaused);
		_playerCharacter.SetPhysicsProcess(!_clock.IsPaused);
		_playerCharacter.SetGameplayInputEnabled(!_clock.IsPaused && _menuState.ActiveMenu is null);
		UpdateStatus();
	}

	private void ResetScenario()
	{
		if (_playerProfile is not { } profile)
		{
			return;
		}

		StartSession(profile, _sessionSeed, "Scenario reset; save slot unchanged.", _survivalGathering, _starterWildlifeAsFar);
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
		_weightChoice.Value = 0.5;
		_muscleChoice.Value = 0.5;
		UpdateCreationPreview();
	}

	private void UpdateCreationPreview()
	{
		if (_creationPreview is null || _heightChoice.Selected < 0 || _bodyTypeChoice.Selected < 0 || _sexChoice.Selected < 0) return;
		try
		{
			var profile = PlayerCharacterProfile.Create(
				"Preview",
				(PlayerHeight)_heightChoice.Selected,
				(PlayerBodyType)_bodyTypeChoice.Selected,
				(PlayerSex)_sexChoice.Selected,
				_weightChoice.Value,
				_muscleChoice.Value);
			var body = PlayerBodyProportions.From(profile);
			_creationPreview.Text = $"Preview: {body.Height:0.00}m tall · torso {body.TorsoWidth:0.00}m wide · limbs {body.LimbThickness:0.00}m";
			foreach (var child in _previewRoot.GetChildren()) child.QueueFree();
			var rig = new PlayerBodyRig(body);
			_previewRoot.AddChild(rig.Root);
			rig.Root.Position = new Vector3(0, -body.Height * 0.5f, 0);
		}
		catch (ArgumentException)
		{
			_creationPreview.Text = "Preview unavailable for the selected values.";
		}
	}

	private void StartNewGame()
	{
		try
		{
			var profile = PlayerCharacterProfile.Create(
				_playerNameInput.Text,
				(PlayerHeight)_heightChoice.Selected,
				(PlayerBodyType)_bodyTypeChoice.Selected,
				(PlayerSex)_sexChoice.Selected,
				_weightChoice.Value,
				_muscleChoice.Value);
			StartSession(profile, NewGameSession.DefaultSeed, "New game started.", survivalGathering: true,
				startStarterWildlifeAsFar: true);
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
		string saveLoadMessage,
		bool survivalGathering = false,
		bool startStarterWildlifeAsFar = false)
	{
		CancelGameplayActions();
		var session = NewGameSession.Create(_speciesCatalog, profile, seed, survivalGathering,
			_wildlifeRegionCatalog, startStarterWildlifeAsFar);
		_sessionActive = false;
		_playerCharacter.SetPhysicsProcess(false);
		ResetTransientServices();
		_clock.Reset();
		_simulationTick = 0;
		ClearEntityViews();
		ClearResourceMarkers();
		ClearFireplaceViews();
		DetachSocialFeedback();
		_world = session.World;
		_wildlifeProximity = new WildlifeProximityCoordinator(_world, _speciesCatalog, _wildlifeRegionCatalog);
		_treeRegrowth = new TreeRegrowthService(_world);
		AttachSocialFeedback();
		_interactionResolver = new InteractionResolver(
			_world,
			_tradeGoodsCatalog,
			new RelationshipEventService(_world, _relationshipEventCatalog));
		_playerCommands = new PlayerCommandBoundary(
			_world,
			_clock,
			new CreatureGreetingService(_world, _speciesCatalog, _relationshipEventCatalog));
		_playerProfile = profile;
		_sessionSeed = seed;
		_survivalGathering = survivalGathering;
		_starterWildlifeAsFar = startStarterWildlifeAsFar;

		_map.Mesh = new PlaneMesh { Size = new Vector2(WorldHalfExtent * 2, WorldHalfExtent * 2) };
		// Keep the large background plane below the town ground and road tops, which
		// share the same Y=0 surface as navigation and actor positions.
		_map.Position = new Vector3(0, -0.01f, 0);
		_map.MaterialOverride = CreateMaterial(new Color(0.32f, 0.5f, 0.24f));
		BuildScenery();
		RebuildWorkbenchViews();
		_navigationRegion.NavigationMesh = CreateNavigationMesh(CurrentWorkbenchPositions(), _world.Structures);
		UpdateFarmPresentation();

		_foodConsumption = new FoodConsumptionService(_world, _speciesCatalog, GetNavigationAdapter);
		_carcassDecay = new CarcassDecayService(_world);
		_creatureHunting = new CreatureHuntingService(_world, _speciesCatalog, GetNavigationAdapter);
		_wildPreyThreat = new WildPreyThreatService(_world, _speciesCatalog, GetNavigationAdapter);
		_wandering = new WanderingService(
			_world,
			GetNavigationAdapter,
			[
				new System.Numerics.Vector3(-5, 0, -3),
				new System.Numerics.Vector3(4, 0, 3),
				new System.Numerics.Vector3(7, 0, -5),
				new System.Numerics.Vector3(-26, 0, 26),
				new System.Numerics.Vector3(26, 0, -26),
				new System.Numerics.Vector3(26, 0, 26)
			],
			_sessionSeed);
		_woodHauling = new WoodHaulingService(_world, GetNavigationAdapter);
		_squad = new SquadOrderService(_world, GetNavigationAdapter);
		_orders = new OrderService(
			_world,
			GetNavigationAdapter,
			entityId => _wildPreyThreat!.SuppressesOtherBehavior(entityId)
				|| IsNpcAnimationTestMoving(entityId));

		RebuildResourceMarkersFromWorld();
		RebuildFireplaceViews();

		var player = session.Player;
		_playerCharacter.GlobalPosition = new Vector3(
			player.Position.X,
			player.Position.Y,
			player.Position.Z);
		_playerCharacter.BindPlayer(
			player.Id,
			profile,
			(float)_speciesCatalog.GetRequired(player.SpeciesId).MovementSpeed);
		_playerCharacter.SetEquippedClothing(player.EquippedItems);
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
		_menuState.Close();
		_saveLoadStatus = saveLoadMessage;
		_sessionActive = true;
		_characterCreation.Visible = false;
		ApplyMenuState();
		UpdateFarmPresentation();
		SyncConstructionPresentation();
		UpdateStatus();
	}

	private void ResetTransientServices()
	{
		_foodConsumption?.Reset();
		_creatureHunting?.Reset();
		_wildPreyThreat?.Reset();
		_wandering?.Reset();
		_woodHauling?.Reset();
		_squad?.Reset();
		_orders?.Reset();
	}

	private void AddEntityView(EntityRecord entity)
	{
		var species = _speciesCatalog.GetRequired(entity.SpeciesId);
		var movementSpeed = (float)species.MovementSpeed;
		var isCreature = CreatureBodyPlanResolver.Resolve(species.BodyCategory, isCreature: true)
			!= CreatureBodyPlan.Humanoid;
		var view = new EntityView { Name = $"View_{entity.Id}" };
		view.Bind(entity.Id, movementSpeed, isCreature, entity.SpeciesId, species.BodyCategory, species.Anchors,
			entity.Sex, _entityPresentationCatalog);
		AddChild(view);
		_entityViews.Add(entity.Id, view);

		var button = new Button { Name = $"Entity_{entity.Id}", Text = entity.Id };
		button.Pressed += () => SelectEntity(entity.Id);
		button.TooltipText = "Select this entity; right-click for individual orders.";
		button.GuiInput += input =>
		{
			if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }
				&& _entityContextMenu is not null
				&& _menuState.ActiveMenu == PlayerMenu.Settlement)
			{
				OpenEntityContextMenu(entity.Id);
				GetViewport().SetInputAsHandled();
			}
		};
		_entityList.AddChild(button);
		_entityButtons.Add(entity.Id, button);
		view.Sync(_world);
	}

	internal EntityPresentationCatalog GetEntityPresentationCatalog() => _entityPresentationCatalog;

	private void SelectEntity(string entityId)
	{
		if (_world.TryGetEntity(entityId, out _))
		{
			_selectedEntityId = entityId;
			ShowSelectedSchedule(entityId);
			UpdateInspector();
			UpdateWorldSelection();
		}
	}

	private void ConfigureSquadControls()
	{
		var controls = GetNode<VBoxContainer>("Controls/Panel/Scroll/Margin/VBox");
		var heading = new Label { Name = "SquadHeading", Text = "Squad: select human NPCs, then add/remove", AutowrapMode = TextServer.AutowrapMode.WordSmart };
		controls.AddChild(heading);
		controls.MoveChild(heading, 7);
		var row = new HBoxContainer { Name = "SquadMembership" };
		controls.AddChild(row);
		controls.MoveChild(row, 8);
		foreach (var add in new[] { true, false })
		{
			var button = new Button { Text = add ? "Add selected" : "Remove member" };
			button.Pressed += () =>
			{
				var result = add ? _world.AddSquadMember(_selectedEntityId) : _world.RemoveSquadMember(_selectedEntityId);
				_lastActionMessage = $"Squad membership: {result}.";
				UpdateStatus();
			};
			row.AddChild(button);
		}
		var targetRow = new HBoxContainer { Name = "SquadTarget" };
		controls.AddChild(targetRow);
		controls.MoveChild(targetRow, 9);
		targetRow.AddChild(new Label { Text = "Move X/Z:" });
		_squadX = new SpinBox { MinValue = -WorldHalfExtent, MaxValue = WorldHalfExtent, Value = 4 };
		_squadZ = new SpinBox { MinValue = -WorldHalfExtent, MaxValue = WorldHalfExtent, Value = 3 };
		targetRow.AddChild(_squadX);
		targetRow.AddChild(_squadZ);
		var orders = new HBoxContainer { Name = "SquadOrders" };
		controls.AddChild(orders);
		controls.MoveChild(orders, 10);
		foreach (var order in new[] { SquadOrderKind.MoveTo, SquadOrderKind.FollowPlayer, SquadOrderKind.Stop })
		{
			var button = new Button { Text = order switch { SquadOrderKind.MoveTo => "Move to", SquadOrderKind.FollowPlayer => "Follow player", _ => "Stop" } };
			button.Pressed += () => IssueSquadOrder(order);
			orders.AddChild(button);
		}
	}

	private void IssueSquadOrder(SquadOrderKind order)
	{
		var target = new System.Numerics.Vector3((float)_squadX.Value, 0, (float)_squadZ.Value);
		_lastActionMessage = _squad!.Execute(order, target).Reason;
		UpdateStatus();
	}

	private void ConfigureIndividualOrderControls()
	{
		var row = new HBoxContainer { Name = "IndividualOrders" };
		row.AddChild(new Label { Text = "Order selected:" });
		_followSelectedOrderButton = new Button { Text = "Follow", TooltipText = "Follow the player's character." };
		_followSelectedOrderButton.Pressed += () => IssueIndividualOrder(_selectedEntityId, "follow");
		row.AddChild(_followSelectedOrderButton);
		_staySelectedOrderButton = new Button { Text = "Stay", TooltipText = "Hold the position where this order is issued." };
		_staySelectedOrderButton.Pressed += () => IssueIndividualOrder(_selectedEntityId, "stay");
		row.AddChild(_staySelectedOrderButton);
		_settlementDetails.AddChild(row);
		_settlementDetails.MoveChild(row, Math.Min(2, _settlementDetails.GetChildCount() - 1));

		_entityContextMenu = new PopupMenu { Name = "EntityContextMenu" };
		_entityContextMenu.AddItem("Follow player", 1);
		_entityContextMenu.AddItem("Stay here", 2);
		_entityContextMenu.IdPressed += id => IssueIndividualOrder(_contextEntityId, id == 1 ? "follow" : "stay");
		_menuHud.AddChild(_entityContextMenu);
	}

	private void OpenEntityContextMenu(string entityId)
	{
		_contextEntityId = entityId;
		SelectEntity(entityId);
		_entityContextMenu.Position = (Vector2I)GetViewport().GetMousePosition();
		_entityContextMenu.Popup();
	}

	private void IssueIndividualOrder(string? targetId, string command)
	{
		if (targetId is null || _world.PlayerEntityId is not { } playerId)
		{
			_lastActionMessage = "Select a mobile entity and a player character first.";
			UpdateStatus();
			return;
		}

		var result = _interactionResolver.Resolve(new InteractionRequest(
			playerId, targetId, InteractionType.Command, command));
		_lastActionMessage = result.Reason;
		_contextEntityId = null;
		_entityContextMenu.Hide();
		UpdateStatus();
	}

	private void AdvanceWoodHauling()
	{
		foreach (var entity in _world.Entities)
		{
			if (string.Equals(entity.Id, _world.PlayerEntityId, StringComparison.Ordinal)
				|| IsNpcAnimationTestMoving(entity.Id)
				|| (_squad?.Order != SquadOrderKind.None && _world.SquadMemberIds.Contains(entity.Id)))
			{
				continue;
			}
			if (_orders?.ShouldSuppressAutonomousBehavior(entity) ?? false) continue;

			_woodHauling!.Advance(
				entity,
				_speciesCatalog.GetRequired(entity.SpeciesId),
				_simulationTick, _clock.SimulationTime);
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
		_wildPreyThreat?.OnEntityRemoved(entityId);

		_entityViews.Remove(entityId, out var view);
		view?.CancelNavigation();
		view?.QueueFree();
		_entityButtons.Remove(entityId, out var button);
		button?.QueueFree();
		_selectedEntityId = null;
		UpdateInspector();
	}

	private void RemoveEntityPresentation(string entityId)
	{
		_wildPreyThreat?.OnEntityRemoved(entityId);
		if (_entityViews.Remove(entityId, out var view))
		{
			view.CancelNavigation();
			view.QueueFree();
		}
		if (_entityButtons.Remove(entityId, out var button)) button.QueueFree();
		if (_selectedEntityId == entityId)
		{
			_selectedEntityId = null;
			UpdateInspector();
		}
	}

	private void SyncEntityViews()
	{
		foreach (var view in _entityViews.Values)
		{
			view.Sync(_world);
		}
	}

	private void SyncPlayerState()
	{
		if (_world.PlayerEntityId is not { } playerId)
		{
			return;
		}

		var position = _playerCharacter.GlobalPosition;
		_world.TrySetEntityPosition(
			playerId,
			new System.Numerics.Vector3(position.X, position.Y, position.Z));
		var forward = -_playerCharacter.GlobalTransform.Basis.Z;
		var facing = new System.Numerics.Vector3(forward.X, 0, forward.Z);
		if (facing.LengthSquared() > 0.0001f)
			_world.SetPlayerFacing(System.Numerics.Vector3.Normalize(facing));
	}

	private void SyncPlayerPosition() => SyncPlayerState();

	private void AdvanceSimulationTick(double tickSeconds)
	{
		_simulationTick = checked(_simulationTick + 1);
		SyncPlayerState();
		UpdateWildlifeProximity();
		AdvanceNeeds(tickSeconds);
		var depletedTreesBefore = _world.ResourcePiles
			.Where(source => source.Id.StartsWith("tree-adult-", StringComparison.Ordinal)
				&& source.Contents.Counts.GetValueOrDefault(ItemIds.Wood) == 0)
			.Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
		_treeRegrowth?.Advance(tickSeconds);
		var depletedTreesAfter = _world.ResourcePiles
			.Where(source => source.Id.StartsWith("tree-adult-", StringComparison.Ordinal)
				&& source.Contents.Counts.GetValueOrDefault(ItemIds.Wood) == 0)
			.Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
		if (!depletedTreesBefore.SetEquals(depletedTreesAfter))
		{
			ClearResourceMarkers();
			RebuildResourceMarkersFromWorld();
		}
		foreach (var entity in _world.Entities)
		{
			// An explicit squad order takes precedence over schedule-driven travel.
			if (IsNpcAnimationTestMoving(entity.Id)
				|| (_squad?.Order != SquadOrderKind.None && _world.SquadMemberIds.Contains(entity.Id)))
				continue;
			_woodHauling!.ReconcileSchedule(entity, _clock.SimulationTime);
		}
		AdvanceNavigationTick(tickSeconds);
		foreach (var expiredId in _carcassDecay!.Advance(tickSeconds))
			RemoveResourceMarker(new(TargetKind.Food, expiredId));
		AdvanceFoodConsumption();
		_squad!.Advance();
		_orders!.Advance();
		AdvanceWoodHauling();
		AdvanceWandering();
		AdvanceNpcAnimationTestRoutes();
	}

	private void UpdateWildlifeProximity()
	{
		if (_world.PlayerEntityId is not { } playerId || !_world.TryGetEntity(playerId, out var player) || player is null)
			return;
		_wildlifeProximity?.Update(new System.Numerics.Vector3(player.Position.X, player.Position.Y, player.Position.Z));
		SyncEntityViewMembership();
	}

	private void ActivateStarterWildlifeForValidation()
	{
		var region = _wildlifeRegionCatalog.Regions.Single();
		var position = new Vector3(region.Center.X, region.Center.Y, region.Center.Z);
		_playerCharacter.GlobalPosition = position;
		_world.TrySetEntityPosition(_world.PlayerEntityId, new System.Numerics.Vector3(position.X, position.Y, position.Z));
		if (!_world.FarPopulations.Any(record => record.RegionId == region.Id)) return;
		_wildlifeProximity!.Update(new System.Numerics.Vector3(position.X, position.Y, position.Z));
		SyncEntityViewMembership();
	}

	private void SyncEntityViewMembership()
	{
		foreach (var entityId in _entityViews.Keys.Where(id => !_world.TryGetEntity(id, out _)).ToArray())
			RemoveEntityPresentation(entityId);
		foreach (var entity in _world.Entities)
			if (entity.Id != _world.PlayerEntityId && !_entityViews.ContainsKey(entity.Id)) AddEntityView(entity);
	}

	private void AdvanceNavigationTick(double tickSeconds)
	{
		// The step boundary uses the live player position, including validation/manual ticks.
		SyncPlayerState();
		foreach (var view in _entityViews.Values)
		{
			view.AdvanceNavigation(tickSeconds, _world, _squad, _orders, ConstrainActorSpacing);
		}
	}

	private System.Numerics.Vector3 ConstrainActorSpacing(string entityId, System.Numerics.Vector3 current, System.Numerics.Vector3 proposed)
	{
		const float minimumSeparation = 0.85f;
		var adjusted = proposed;
		foreach (var other in _world.Entities)
		{
			if (other.Id == entityId || other.Health.IsDead) continue;
			var dx = adjusted.X - other.Position.X;
			var dz = adjusted.Z - other.Position.Z;
			var distanceSquared = dx * dx + dz * dz;
			if (distanceSquared >= minimumSeparation * minimumSeparation) continue;
			var currentDx = current.X - other.Position.X;
			var currentDz = current.Z - other.Position.Z;
			var currentDistanceSquared = currentDx * currentDx + currentDz * currentDz;
			if (currentDistanceSquared >= minimumSeparation * minimumSeparation) return current;
			var distance = MathF.Sqrt(MathF.Max(distanceSquared, 0.0001f));
			adjusted = new System.Numerics.Vector3(
				other.Position.X + dx / distance * minimumSeparation,
				proposed.Y,
				other.Position.Z + dz / distance * minimumSeparation);
		}
		return adjusted;
	}

	private void AdvanceNeeds(double simulationSeconds)
	{
		foreach (var entity in _world.Entities)
		{
			entity.Health.AdvanceTime(simulationSeconds);
			entity.AdvanceNeeds(simulationSeconds, isSleeping: entity.SleepState == SleepState.Sleeping);
		}
	}

	private void AdvanceFoodConsumption()
	{
		foreach (var entity in _world.Entities.ToArray())
		{
			if (string.Equals(entity.Id, _world.PlayerEntityId, StringComparison.Ordinal))
			{
				continue;
			}
			if (IsNpcAnimationTestMoving(entity.Id)) continue;

			var threatResult = _wildPreyThreat!.Advance(entity, _simulationTick);
			if (threatResult.Status is WildPreyThreatStatus.Fleeing or WildPreyThreatStatus.Waiting)
				continue;
			if (_orders?.ShouldSuppressAutonomousBehavior(entity) ?? false)
				continue;

			if (_creatureHunting!.IsHunting(entity.Id))
			{
				HandleHuntingResult(_creatureHunting.Advance(entity, _simulationTick));
				continue;
			}

			var foodResult = _foodConsumption!.Advance(
				entity,
				_speciesCatalog.GetRequired(entity.SpeciesId),
				_simulationTick);
			if (foodResult.Status is FoodConsumptionStatus.Traveling or FoodConsumptionStatus.Ate)
				continue;

			HandleHuntingResult(_creatureHunting.Advance(entity, _simulationTick));
		}
	}

	private void HandleHuntingResult(CreatureHuntingResult result)
	{
		if (result.Status != CreatureHuntingStatus.Harvested
			|| result.TargetEntityId is not { } preyId
			|| result.Carcass is not { } carcass)
			return;

		RemoveEntityPresentation(preyId);
		AddResourceMarker(
			new(TargetKind.Food, carcass.Id),
			carcass.Position,
			new Color(0.55f, 0.18f, 0.12f));
	}

	private void AdvanceWandering()
	{
		foreach (var entity in _world.Entities)
		{
			if (string.Equals(entity.Id, _world.PlayerEntityId, StringComparison.Ordinal)
				|| IsNpcAnimationTestMoving(entity.Id)
				|| (_squad?.Order != SquadOrderKind.None && _world.SquadMemberIds.Contains(entity.Id))
				|| _wildPreyThreat!.SuppressesOtherBehavior(entity.Id))
			{
				continue;
			}
			if (_orders?.ShouldSuppressAutonomousBehavior(entity) ?? false) continue;

			_wandering!.Advance(
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
		ResetNpcAnimationTestRoutes();
		_npcAnimationTestPanel?.Close();
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
		foreach (var marker in _resourceMarkers.Values)
		{
			marker.QueueFree();
		}

		_resourceMarkers.Clear();
	}

	private void RemoveResourceMarker(WorldTarget target)
	{
		if (!_resourceMarkers.Remove(target, out var marker)) return;
		marker.QueueFree();
	}

	private void ClearFireplaceViews()
	{
		foreach (var view in _fireplaceViews.Values) view.QueueFree();
		_fireplaceViews.Clear();
	}

	private void RebuildFireplaceViews()
	{
		ClearFireplaceViews();
		foreach (var fire in _world.Fireplaces)
		{
			var view = new MeshInstance3D
			{
				Name = fire.Id,
				Mesh = new CylinderMesh { TopRadius = 0.35f, BottomRadius = 0.45f, Height = 0.25f },
				Position = new Vector3(fire.Position.X, fire.Position.Y + 0.12f, fire.Position.Z),
				MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.25f, 0.05f), EmissionEnabled = true, Emission = new Color(0.5f, 0.08f, 0.01f) }
			};
			AddChild(view); _fireplaceViews[fire.Id] = view;
		}
	}

	private void RebuildResourceMarkersFromWorld()
	{
		foreach (var source in _world.FoodSources)
		{
			AddResourceMarker(new(TargetKind.Food, source.Id), source.Position, new Color(0.75f, 0.6f, 0.15f));
		}

		foreach (var source in _world.ResourcePiles)
		{
			var target = new WorldTarget(TargetKind.ResourcePile, source.Id);
			if (source.Id.StartsWith("tree-adult-", StringComparison.Ordinal))
			{
				var available = source.Contents.Counts.GetValueOrDefault(ItemIds.Wood) > 0;
				AddResourceMarker(target, source.Position, available ? new Color(0.16f, 0.48f, 0.18f) : new Color(0.3f, 0.2f, 0.12f), tree: available);
			}
			else if (source.Id.StartsWith("rock-loose-", StringComparison.Ordinal))
				AddResourceMarker(target, source.Position, new Color(0.42f, 0.44f, 0.46f), rock: true);
			else
				AddResourceMarker(target, source.Position, new Color(0.45f, 0.25f, 0.1f));
		}

		foreach (var stockpile in _world.Stockpiles)
		{
			AddResourceMarker(new(TargetKind.Stockpile, stockpile.Id), stockpile.Position, new Color(0.2f, 0.45f, 0.25f));
		}
	}

	private void AddResourceMarker(WorldTarget target, System.Numerics.Vector3 position, Color color, bool tree = false, bool rock = false)
	{
		Mesh mesh = tree
			? new CylinderMesh { TopRadius = 0.65f, BottomRadius = 0.8f, Height = 3.2f }
			: rock ? new SphereMesh { Radius = 0.45f, Height = 0.7f } : new BoxMesh { Size = new Vector3(1, 0.6f, 1) };
		var marker = new MeshInstance3D
		{
			Name = $"{target.Kind}_{target.Id}",
			Mesh = mesh,
			Position = new Vector3(position.X, tree ? 1.6f : rock ? 0.35f : 0.3f, position.Z),
			MaterialOverride = new StandardMaterial3D { AlbedoColor = color }
		};
		AddChild(marker);
		_resourceMarkers.Add(target, marker);
	}

	private string TryPlayerEat()
	{
		if (_foodConsumption is null || _world.PlayerEntityId is not { } playerId
			|| !_world.TryGetEntity(playerId, out var player) || player is null)
		{
			return "Eating is unavailable.";
		}

		var nearest = _world.FoodSources
			.OrderBy(source => System.Numerics.Vector3.Distance(player.Position, source.Position))
			.FirstOrDefault();
		var result = _foodConsumption.TryPlayerEat(nearest?.Id);
		return result.Status == PlayerEatStatus.Ate
			? $"{result.Reason} Satiety {result.Satiety:0.#}."
			: result.Reason;
	}

	private string GreetSelectedEntity()
	{
		SyncPlayerState();
		return _playerCommands?.Execute(new PlayerCommand(PlayerCommandKind.Interact, _selectedEntityId)).Reason
			?? "Friendly interaction is not initialized.";
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
		var playerWarmth = _world.PlayerEntityId is { } playerId
			? new FireplaceService(_world).IsWarm(playerId) ? "Warm by lit fireplace" : "No nearby warmth"
			: "Unavailable";
		_statusLabel.Text =
			$"Simulation time: {_clock.SimulationTime:0.0}s | Day time: {ScheduleTime(DailyScheduleState.DaySeconds(_clock.SimulationTime) / DailyScheduleState.SecondsPerHour)}\nState: {state}\nSpeed: {(int)_clock.Speed}x\n" +
			playerStatus +
			$"Food: {foodConsumption.FoodUnitsRemaining} remaining, {foodConsumption.FoodUnitsConsumed} consumed\n" +
			$"Wood: ground {groundWood}, carried {carriedWood}, stored {storedWood} (total {totalWood})\n" +
			$"Warmth: {playerWarmth}\n" +
			$"Save/load: {_saveLoadStatus}\n" +

			$"Menu: {_menuState.ActiveMenu?.ToString() ?? "closed"} | Change button mapping in Save / Settings.\n" +
			$"Last action: {_lastActionMessage}\n" +
			$"Squad: {string.Join(", ", _world.SquadMemberIds)} | {_squad?.Order}\n" +
			"Select an entity; F5 route, F6 unreachable, F7 cancel, F8 remove goal";
		_pauseButton.Text = _clock.IsPaused ? "Resume" : "Pause";
		UpdatePlayerMenuContents();
		UpdateWorldSelection();
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
			_applySchedule.Disabled = true;
			if (_followSelectedOrderButton is not null) _followSelectedOrderButton.Disabled = true;
			if (_staySelectedOrderButton is not null) _staySelectedOrderButton.Disabled = true;
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
		var species = _speciesCatalog.GetRequired(entity.SpeciesId);
		var canOrder = entity.Id != _world.PlayerEntityId && !entity.Health.IsDead
			&& !entity.Health.IsIncapacitated && species.MovementSpeed > 0;
		_followSelectedOrderButton.Disabled = !canOrder;
		_staySelectedOrderButton.Disabled = !canOrder;
		var orderSummary = entity.OrderIntent switch
		{
			{ Kind: EntityOrderKind.Follow } order => $"Follow {order.IssuerId}",
			{ Kind: EntityOrderKind.Stay, Position: { } position } => $"Stay at ({position.X:0.0}, {position.Y:0.0}, {position.Z:0.0})",
			_ => "None"
		};
		var temperament = IndividualTemperament.ForEntity(entity.Id, species.Temperament);
		var capabilities = string.Join(", ", new[]
		{
			species.CanHaul ? "haul" : null,
			species.CanUseTools ? "tools" : null,
			species.CanOpenDoors ? "doors" : null,
			species.CanTrack ? "track" : null,
			species.CanAcceptFriendlyInteraction ? "friendly" : null
		}.Where(name => name is not null));
		if (capabilities.Length == 0) capabilities = "none";
		var bodyHealth = string.Join(", ", entity.Health.Parts.Select(part => $"{part.Name} {part.CurrentHealth:0}/{part.MaximumHealth:0}"));
		var inspection = EntityInspectionReadModel.Create(_world, entity);
		var relationships = entity.Id == _world.PlayerEntityId ? "not applicable" : EntityInspectionReadModel.FormatOutgoingRelationships(inspection, includeValues: false);
		var scheduled = _world.HasDailySchedule(entity);
		_applySchedule.Disabled = !scheduled || entity.Health.IsDead;
		var scheduleText = "Schedule: not applicable\n";
		if (scheduled)
		{
			var schedule = _world.GetWorkSchedule(entity.Id);
			var phase = schedule.Evaluate(_clock.SimulationTime);
			scheduleText = $"Work hours: {schedule.StartHour:00}:00–{schedule.EndHour:00}:00\n" +
				$"Schedule: {(phase.IsWorking ? "Work" : "Rest")} | Next: {ScheduleTime(phase.NextTransitionHour)} in {phase.SecondsUntilTransition:0.0}s\n" +
				"Hunger and squad orders take priority; carried wood can finish delivery.\n";
		}
		_inspectorLabel.Text =
			$"ID: {entity.Id}\n" +
			$"Species: {species.DisplayName} ({species.BodyCategory}, {species.DietaryProfile})\n" +
			$"Traits: boldness {temperament.Boldness:0.0}, aggression {temperament.Aggression:0.0}, fear {temperament.Fear:0.0}, " +
			$"curiosity {temperament.Curiosity:0.0}, sociability {temperament.Sociability:0.0}, territoriality {temperament.Territoriality:0.0}\n" +
			$"Capabilities: {capabilities}\n" +
			$"Body: {bodyHealth}\n" +
			$"Captivity: {entity.CaptivityState}\n" +
			$"Strength/Endurance/Dexterity: {species.Strength:0.0}/{species.Endurance:0.0}/{species.Dexterity:0.0}\n" +
			$"Position: {entity.Position}\n" +
			$"Individual order: {orderSummary}\n" +
			$"Satiety: {entity.Satiety:0.0}\n" +
			$"Needs: {needState}\n" +
			$"Activity: {entity.Activity}\n" +
			scheduleText +
			$"Squad member: {_world.SquadMemberIds.Contains(entity.Id)} | Order: {_squad?.Order}\n" +
			$"Target: {target}\n" +
			$"Navigation: {navigationStatus}\n" +
			$"Inventory: {inventory}\n" +
			$"Waiting reason: {entity.WaitingReason ?? "none"}\n" +
			$"Outgoing relationships:\n{relationships}";
		_removeSelectedButton.Disabled = false;
	}

	private void SaveScenario()
	{
		SyncPlayerState();
		CancelGameplayActions();
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
			_saveLoadStatus = $"Save failed: {exception.Message}{(exception.InnerException is null ? string.Empty : $" ({exception.InnerException.Message})")}";
		}

		UpdateStatus();
	}

	private void LoadScenario()
	{
		CancelGameplayActions();
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
				_creatureHunting!,
				_wandering!,
				_woodHauling!,
				_squad!,
				_wildPreyThreat!,
				_orders!);
			_clock.Restore(restoredTime);
			_wildlifeProximity = new WildlifeProximityCoordinator(_world, _speciesCatalog, _wildlifeRegionCatalog);
			_simulationTick = checked((long)Math.Round(
				restoredTime / SimulationClock.TickIntervalSeconds,
				MidpointRounding.AwayFromZero));
			RebuildPresentationFromWorld();
			_saveLoadStatus = $"Loaded {_clock.SimulationTime:0.0}s; paused for inspection.";
			_lastActionMessage = "Loaded saved orders; navigation routes are rebuilding.";
		}
		catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
		{
			_saveLoadStatus = $"Load failed: {exception.Message}";
		}

		UpdateStatus();
	}

	private void RebuildPresentationFromWorld()
	{
		RebuildWorkbenchViews();
		_navigationRegion.NavigationMesh = CreateNavigationMesh(CurrentWorkbenchPositions(), _world.Structures);
		ClearEntityViews();
		ClearResourceMarkers();
		RebuildResourceMarkersFromWorld();
		RebuildFireplaceViews();
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
			_playerCharacter.SetEquippedClothing(player.EquippedItems);
			var facing = new Vector3(_world.PlayerFacing.X, 0, _world.PlayerFacing.Z);
			if (facing.LengthSquared() > 0.0001f)
				_playerCharacter.Rotation = new Vector3(0, Mathf.Atan2(-facing.X, -facing.Z), 0);
			_playerCharacter.Visible = true;
			_playerCharacter.SetProcessUnhandledInput(true);
			_playerCharacter.SetPhysicsProcess(false);
		}
		else
		{
			_playerCharacter.Visible = false;
			_playerCharacter.SetProcessUnhandledInput(false);
		}

		foreach (var entity in _world.Entities)
		{
			if (!string.Equals(entity.Id, _world.PlayerEntityId, StringComparison.Ordinal))
			{
				AddEntityView(entity);
			}
		}
	}



	private static bool IsBlockedCell(float minX, float maxX, float minZ, float maxZ,
		IEnumerable<System.Numerics.Vector3>? workbenchPositions = null, IEnumerable<Structure>? structures = null,
		IReadOnlyList<NavigationBlocker>? structureBlockers = null)
	{
		if (minX < 1f && maxX > -1f && minZ < 1f && maxZ > -1f)
		{
			return true;
		}

		const float workbenchNavigationClearance = 2f;
		var workbenches = workbenchPositions ?? [NewGameSession.TownWorkbenchPosition];
		if (workbenches.Any(workbench =>
			minX < workbench.X + workbenchNavigationClearance && maxX > workbench.X - workbenchNavigationClearance
			&& minZ < workbench.Z + workbenchNavigationClearance && maxZ > workbench.Z - workbenchNavigationClearance))
		{
			return true;
		}

		if (IsBuildingBand(minX, maxX) && IsBuildingBand(minZ, maxZ)) return true;

		return (structureBlockers ?? StructureNavigationBlockers(structures).ToArray()).Any(blocker =>
			minX < blocker.MaxX && maxX > blocker.MinX && minZ < blocker.MaxZ && maxZ > blocker.MinZ);
	}

	private static bool IsBuildingBand(float min, float max) =>
		BuildingBands.Any(band => min >= band.Min && max <= band.Max);

	private readonly record struct NavigationBlocker(float MinX, float MaxX, float MinZ, float MaxZ);

	private static IEnumerable<NavigationBlocker> StructureNavigationBlockers(IEnumerable<Structure>? structures)
	{
		foreach (var structure in structures ?? [])
		{
			if (structure.Type != StructureType.Hut) continue;
			var x = structure.Position.X;
			var z = structure.Position.Z;
			// Match the completed hut's wall segments while leaving its open doorway traversable.
			yield return new(x - 2f, x - 1.8f, z - 2f, z + 2f);
			yield return new(x + 1.8f, x + 2f, z - 2f, z + 2f);
			yield return new(x - 2f, x + 2f, z - 2f, z - 1.8f);
			yield return new(x - 2f, x - 0.5f, z + 1.8f, z + 2f);
			yield return new(x + 0.5f, x + 2f, z + 1.8f, z + 2f);
			if (structure.IsClosed)
				yield return new(x - 0.5f, x + 0.5f, z + 1.8f, z + 2f);
		}
	}

	private static StandardMaterial3D CreateMaterial(Color color) => new() { AlbedoColor = color };

	private void RebuildWorkbenchViews()
	{
		foreach (var view in _workbenchViews.Values.Where(view => view != _workbench).ToArray())
		{
			RemoveChild(view);
			view.QueueFree();
		}
		_workbenchViews.Clear();

		var starter = _world.Workstations.FirstOrDefault(workstation => workstation.Id == "workbench-1");
		_workbench.Visible = starter is not null;
		if (starter is not null)
		{
			BuildWorkbenchView(_workbench, starter.Position);
			_workbenchViews.Add(starter.Id, _workbench);
		}

		foreach (var workstation in _world.Workstations.Where(workstation => workstation.Id != "workbench-1"))
			AddWorkbenchView(workstation);
	}

	private void AddWorkbenchView(Workstation workstation)
	{
		if (_workbenchViews.ContainsKey(workstation.Id)) return;
		var view = new MeshInstance3D { Name = workstation.Id };
		AddChild(view);
		BuildWorkbenchView(view, workstation.Position);
		_workbenchViews.Add(workstation.Id, view);
	}

	private IEnumerable<System.Numerics.Vector3> CurrentWorkbenchPositions()
		=> _world.Workstations.Select(workstation => workstation.Position).ToArray();

	private void BuildWorkbenchView(MeshInstance3D view, System.Numerics.Vector3 placement)
	{
		foreach (var child in view.GetChildren())
		{
			view.RemoveChild(child);
			child.QueueFree();
		}
		view.Position = new Vector3(placement.X, placement.Y + 1.05f, placement.Z);
		view.Mesh = new BoxMesh { Size = new Vector3(2, 0.18f, 1.4f) };
		view.MaterialOverride = CreateMaterial(new Color(0.52f, 0.31f, 0.15f));
		view.Visible = true;
		// The visual workbench is also a real obstacle. Keep the collision footprint
		// broad enough to cover the legs and shelf while leaving walkable space around it.
		var workbenchBody = new StaticBody3D { Name = "WorkbenchCollision" };
		// Presentation and camera occlusion use layer 2. The player still collides
		// with it through its layer-3 mask, while the camera can pull in cleanly
		// before the tabletop fills the view.
		workbenchBody.CollisionLayer = 2;
		workbenchBody.CollisionMask = 1;
		workbenchBody.AddChild(new CollisionShape3D
		{
			Shape = new BoxShape3D { Size = new Vector3(2f, 1.05f, 1.4f) },
			Position = new Vector3(0, -0.48f, 0)
		});
		view.AddChild(workbenchBody);
		var wood = new Color(0.32f, 0.19f, 0.09f);
		var steel = new Color(0.35f, 0.38f, 0.42f);
		foreach (var x in new[] { -0.8f, 0.8f })
			foreach (var z in new[] { -0.5f, 0.5f })
				AddBenchPart(view, $"Leg_{x}_{z}", new(0.18f, 0.83f, 0.18f), new(x, -0.53f, z), wood);
		AddBenchPart(view, "Shelf", new(1.7f, 0.08f, 1.05f), new(0, -0.63f, 0), wood);
		AddBenchPart(view, "Vise", new(0.32f, 0.24f, 0.3f), new(0.65f, 0.21f, 0.35f), steel);
		AddBenchPart(view, "ViseHandle", new(0.48f, 0.05f, 0.05f), new(0.65f, 0.15f, 0.55f), steel);
		AddBenchPart(view, "Workpiece", new(1.1f, 0.05f, 0.22f), new(-0.1f, 0.115f, -0.3f), new(0.73f, 0.53f, 0.3f));
		AddBenchPart(view, "HammerHandle", new(0.06f, 0.05f, 0.42f), new(-0.55f, 0.115f, 0.22f), wood);
		AddBenchPart(view, "HammerHead", new(0.26f, 0.1f, 0.1f), new(-0.55f, 0.14f, 0.04f), steel);
		view.AddChild(new Label3D { Name = "WorkbenchLabel", Text = "Workbench",
			Position = new Vector3(0, 0.9f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FontSize = 28, PixelSize = 0.006f });
	}

	private static void AddBenchPart(MeshInstance3D view, string name, Vector3 size, Vector3 position, Color color)
		=> view.AddChild(new MeshInstance3D { Name = name, Mesh = new BoxMesh { Size = size },
			Position = position, MaterialOverride = CreateMaterial(color) });

	private void AddScenery(string name, Vector3 size, Vector3 position, Color color)
	{
		_scenery!.AddChild(new MeshInstance3D
		{
			Name = name,
			Mesh = new BoxMesh { Size = size },
			Position = position,
			MaterialOverride = CreateMaterial(color)
		});
	}

	private void AddSolidScenery(string name, Vector3 size, Vector3 position, Color color)
	{
		AddScenery(name, size, position, color);
		var body = new StaticBody3D
		{
			Name = $"{name}Collision", Position = position, CollisionLayer = 2, CollisionMask = 1
		};
		body.AddChild(new CollisionShape3D
		{
			Name = "CollisionShape3D", Shape = new BoxShape3D { Size = size }
		});
		_scenery!.AddChild(body);
	}

	// Town: paved ground, crossing roads, and building blocks matching the navigation grid holes.
	// Countryside: field patches and tree clumps scattered beyond the town edge.
	private void BuildScenery()
	{
		if (_scenery is not null)
		{
			RemoveChild(_scenery);
			_scenery.QueueFree();
		}

		_scenery = new Node3D { Name = "Scenery" };
		AddChild(_scenery);

		AddScenery("TownGround", new Vector3(TownHalfExtent * 2, 0.05f, TownHalfExtent * 2),
			new Vector3(0, -0.025f, 0), new Color(0.62f, 0.62f, 0.6f));
		AddScenery("RoadEastWest", new Vector3(TownHalfExtent * 2, 0.05f, 8), new Vector3(0, -0.024f, 0),
			new Color(0.2f, 0.2f, 0.22f));
		AddScenery("RoadNorthSouth", new Vector3(8, 0.05f, TownHalfExtent * 2), new Vector3(0, -0.024f, 0),
			new Color(0.2f, 0.2f, 0.22f));
		AddScenery("CountryRoadEast", new Vector3(WorldHalfExtent - TownHalfExtent, 0.05f, 6),
			new Vector3((WorldHalfExtent + TownHalfExtent) / 2, -0.024f, 0), new Color(0.5f, 0.4f, 0.28f));
		AddScenery("CountryRoadWest", new Vector3(WorldHalfExtent - TownHalfExtent, 0.05f, 6),
			new Vector3(-(WorldHalfExtent + TownHalfExtent) / 2, -0.024f, 0), new Color(0.5f, 0.4f, 0.28f));

		var index = 0;
		foreach (var (minX, maxX) in BuildingBands)
		{
			foreach (var (minZ, maxZ) in BuildingBands)
			{
				var height = 6f + ((index * 7) % 5) * 3f;
				AddSolidScenery($"Building_{index}", new Vector3(maxX - minX, height, maxZ - minZ),
					new Vector3((minX + maxX) / 2, height / 2, (minZ + maxZ) / 2),
					new Color(0.55f + (index % 3) * 0.08f, 0.5f, 0.45f));
				index++;
			}
		}

		var random = new RandomNumberGenerator { Seed = 2026 };
		for (var field = 0; field < 24; field++)
		{
			var position = RuralPoint(random);
			AddScenery($"Field_{field}", new Vector3(random.RandfRange(14, 30), 0.04f, random.RandfRange(14, 30)),
				new Vector3(position.X, 0.03f, position.Y),
				field % 2 == 0 ? new Color(0.72f, 0.64f, 0.25f) : new Color(0.2f, 0.42f, 0.18f));
		}

		for (var tree = 0; tree < 120; tree++)
		{
			var position = RuralPoint(random);
			AddScenery($"TreeTrunk_{tree}", new Vector3(0.6f, 3, 0.6f),
				new Vector3(position.X, 1.5f, position.Y), new Color(0.35f, 0.22f, 0.1f));
			AddScenery($"TreeCrown_{tree}", new Vector3(3, 3, 3),
				new Vector3(position.X, 4.2f, position.Y), new Color(0.1f, 0.35f, 0.12f));
		}
	}

	// Keeps rural props off the town and the country roads so they never sit on spawn/travel areas.
	private static Vector2 RuralPoint(RandomNumberGenerator random)
	{
		while (true)
		{
			var point = new Vector2(
				random.RandfRange(-WorldHalfExtent + 8, WorldHalfExtent - 8),
				random.RandfRange(-WorldHalfExtent + 8, WorldHalfExtent - 8));
			if ((Mathf.Abs(point.X) > TownHalfExtent + 12 || Mathf.Abs(point.Y) > TownHalfExtent + 12)
				&& Mathf.Abs(point.Y) > 8)
			{
				return point;
			}
		}
	}

	private static NavigationMesh CreateNavigationMesh(IEnumerable<System.Numerics.Vector3>? workbenchPositions = null,
		IEnumerable<Structure>? structures = null)
	{
		var workbenches = (workbenchPositions ?? [NewGameSession.TownWorkbenchPosition]).ToArray();
		var structureBlockers = StructureNavigationBlockers(structures).ToArray();
		// Fixture placement snaps to whole metres, so add exact clearance edges to
		// the grid instead of dropping an entire large town cell. Structure wall edges
		// are also boundaries, so wall cells can be omitted while doorway cells remain.
		var navigationGridLines = NavigationGridLines
			.Concat(workbenches.SelectMany(workbench => new[]
			{
				workbench.X - 2f, workbench.X + 2f, workbench.Z - 2f, workbench.Z + 2f
			}).Select(value => (float)Mathf.RoundToInt(value)))
			.Concat(structureBlockers.SelectMany(blocker => new[]
			{
				blocker.MinX, blocker.MaxX, blocker.MinZ, blocker.MaxZ
			}))
			.Distinct()
			.OrderBy(value => value)
			.ToArray();
		var mesh = new NavigationMesh();
		var vertices = new List<Vector3>();
		var vertexIndices = new Dictionary<(float X, float Z), int>();
		var polygons = new List<int[]>();

		int GetVertex(float x, float z)
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

		void AddRectangle(float minX, float maxX, float minZ, float maxZ)
		{
			polygons.Add(
			[
				GetVertex(minX, minZ),
				GetVertex(minX, maxZ),
				GetVertex(maxX, maxZ),
				GetVertex(maxX, minZ)
			]);
		}

		for (var xi = 0; xi < navigationGridLines.Length - 1; xi++)
		{
			for (var zi = 0; zi < navigationGridLines.Length - 1; zi++)
			{
				var minX = navigationGridLines[xi];
				var maxX = navigationGridLines[xi + 1];
				var minZ = navigationGridLines[zi];
				var maxZ = navigationGridLines[zi + 1];
				if (IsBlockedCell(minX, maxX, minZ, maxZ, workbenches, structures, structureBlockers))
				{
					continue;
				}

				AddRectangle(minX, maxX, minZ, maxZ);
			}
		}
		mesh.SetVertices(vertices.ToArray());
		foreach (var polygon in polygons)
		{
			mesh.AddPolygon(polygon);
		}

		return mesh;
	}
}
