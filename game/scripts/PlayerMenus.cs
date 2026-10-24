using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
	private CanvasLayer _menuHud = null!;
	private Control _inventoryPanel = null!;
	private Control _settingsPanel = null!;
	private Label _characterIdentity = null!;
	private Label _characterStatus = null!;
	private Label _characterAttributes = null!;
	private Label _equipmentSummary = null!;
	private Label _carriedItems = null!;
	private Label _otherCharacterInfo = null!;
	private Label _settingsStatus = null!;
	private Label _bindingStatus = null!;
	private Label _menuHint = null!;
	private TabContainer _inventoryTabs = null!;
	private TabContainer _settingsTabs = null!;
	private TabBar _settlementTabs = null!;
	private OptionButton _itemCategory = null!;
	private VBoxContainer _settlementDetails = null!;
	private PlayerButtonBindings _buttonBindings = null!;
	private readonly Dictionary<(string Action, bool Controller), Button> _mappingButtons = [];
	private (string Action, bool Controller)? _captureBinding;

	private void ConfigurePlayerMenus()
	{
		_buttonBindings = new(ProjectSettings.GlobalizePath("user://player-settings.json"));
		if (!OS.GetCmdlineUserArgs().Any(arg => arg.StartsWith("--validate-", StringComparison.Ordinal)))
			_buttonBindings.Load();
		_menuHud = new CanvasLayer { Name = "PlayerMenus", Layer = 2 };
		AddChild(_menuHud);
		var navigation = new HBoxContainer { Position = new Vector2(20, 12) };
		_menuHud.AddChild(navigation);
		foreach (var menu in Enum.GetValues<PlayerMenu>())
		{
			var button = new Button { Text = menu == PlayerMenu.Settings ? "Save / Settings" : menu.ToString(), FocusMode = Control.FocusModeEnum.None };
			button.Pressed += () => ToggleMenu(menu);
			navigation.AddChild(button);
		}
		_menuHint = new Label { Position = new Vector2(20, 52), MouseFilter = Control.MouseFilterEnum.Ignore };
		_menuHud.AddChild(_menuHint);

		var inventory = CreateMenuPanel("Inventory", out _inventoryPanel);
		_characterIdentity = MenuLabel();
		inventory.AddChild(_characterIdentity);
		_inventoryTabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		inventory.AddChild(_inventoryTabs);
		_characterStatus = AddTextTab(_inventoryTabs, "Status");
		var statusContent = _characterStatus.GetParent();
		var statusColumns = new HBoxContainer();
		statusColumns.AddThemeConstantOverride("separation", 28);
		statusContent.AddChild(statusColumns);
		_characterStatus.Reparent(statusColumns);
		_characterStatus.CustomMinimumSize = new Vector2(300, 0);
		_characterAttributes = MenuLabel();
		_characterAttributes.CustomMinimumSize = new Vector2(220, 0);
		statusColumns.AddChild(_characterAttributes);
		_equipmentSummary = AddTextTab(_inventoryTabs, "Equipment");
		_carriedItems = AddTextTab(_inventoryTabs, "Items");
		_itemCategory = new OptionButton();
		foreach (var category in new[] { "All items", "Food", "Materials", "Currency" }) _itemCategory.AddItem(category);
		_carriedItems.GetParent().AddChild(_itemCategory);
		_carriedItems.GetParent().MoveChild(_itemCategory, 0);
		_itemCategory.ItemSelected += _ => UpdatePlayerMenuContents();
		_otherCharacterInfo = AddTextTab(_inventoryTabs, "Other");
		var slots = new GridContainer { Columns = 3 };
		slots.AddThemeConstantOverride("h_separation", 12);
		slots.AddThemeConstantOverride("v_separation", 12);
		_equipmentSummary.GetParent().AddChild(slots);
		foreach (var slot in new[] { "Head", "Body", "Legs", "Feet", "Accessories" })
			slots.AddChild(new Button { Text = $"{slot}\nPlanned", Disabled = true, CustomMinimumSize = new Vector2(170, 64), TooltipText = "Wearable equipment is planned; this slot is not usable yet." });
		var toolActions = new HBoxContainer { Name = "ToolActions" };
		_equipmentSummary.GetParent().AddChild(toolActions);
		AddMenuButton(toolActions, "Craft axe", CraftPlayerAxe);
		AddMenuButton(toolActions, "Equip axe", EquipPlayerAxe);

		var settings = CreateMenuPanel("Save / Settings", out _settingsPanel);
		_settingsTabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		settings.AddChild(_settingsTabs);
		var saves = AddScrollTab(_settingsTabs, "Game");
		_settingsStatus = MenuLabel();
		saves.AddChild(_settingsStatus);
		AddMenuButton(saves, "Save game (pauses)", SaveScenario);
		AddMenuButton(saves, "Load game (pauses)", LoadScenario);
		AddMenuButton(saves, "Reset current scenario", ResetScenario);
		saves.AddChild(MenuLabel("Save and load use the existing local save slot. Reset starts this character's scenario again; it preserves the save slot."));
		var mapping = AddScrollTab(_settingsTabs, "Controls");
		_bindingStatus = MenuLabel(_buttonBindings.Status);
		mapping.AddChild(_bindingStatus);
		mapping.AddChild(MenuLabel("Mouse or right stick looks. Escape clears the target lock, then closes menus or cancels mapping. Pad B closes menus. Left stick moves."));
		var mappingHeader = new HBoxContainer();
		mappingHeader.AddChild(new Label { Text = "Action", CustomMinimumSize = new Vector2(160, 0) });
		foreach (var columnTitle in new[] { "Keyboard / Mouse", "Controller" })
			mappingHeader.AddChild(new Label { Text = columnTitle, HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(155, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		mapping.AddChild(mappingHeader);
		foreach (var (action, label) in PlayerButtonBindings.Actions)
		{
			var row = new HBoxContainer();
			mapping.AddChild(row);
			row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(160, 0) });
			foreach (var controller in new[] { false, true })
			{
				var button = new Button { CustomMinimumSize = new Vector2(155, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
				button.Pressed += () =>
				{
					_captureBinding = (action, controller);
					_bindingStatus.Text = $"Mapping {label}: press {(controller ? "a controller button" : "a key or mouse button")}. Escape cancels.";
				};
				_mappingButtons.Add((action, controller), button);
				row.AddChild(button);
			}
		}
		AddMenuButton(mapping, "Restore default button mapping", () =>
		{
			_captureBinding = null;
			_buttonBindings.RestoreDefaults(out var reason);
			_bindingStatus.Text = reason;
			RefreshMappingButtons();
		});
		AddPlannedSettingsTab("Audio", "Volume and audio options are planned; this build has no audio settings.");
		AddPlannedSettingsTab("Display", "Display quality and accessibility options are planned; this build uses project defaults.");
		RefreshMappingButtons();

		// Keep the established settlement controls and signals, with fewer controls visible per tab.
		_controlsPanel.AnchorLeft = 0.12f;
		_controlsPanel.AnchorRight = 0.88f;
		_controlsPanel.OffsetLeft = 20;
		_controlsPanel.OffsetTop = 92;
		_controlsPanel.OffsetRight = -20;
		var root = GetNode<VBoxContainer>("Controls/Panel/Scroll/Margin/VBox");
		var title = new HBoxContainer { Name = "MenuTitle" };
		title.AddChild(new Label { Text = "SETTLEMENT", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		AddMenuButton(title, "Close", ClosePlayerMenus);
		root.AddChild(title);
		root.MoveChild(title, 0);
		_settlementTabs = new TabBar { Name = "MenuTabs" };
		foreach (var tab in new[] { "Overview", "People", "Squad" }) _settlementTabs.AddTab(tab);
		root.AddChild(_settlementTabs);
		root.MoveChild(_settlementTabs, 1);
		var people = new HBoxContainer { Name = "PeopleDetails" };
		people.AddThemeConstantOverride("separation", 24);
		root.AddChild(people);
		var list = new VBoxContainer { CustomMinimumSize = new Vector2(190, 0) };
		people.AddChild(list);
		root.GetNode<Control>("EntityListHeading").Reparent(list);
		var listScroll = new ScrollContainer { CustomMinimumSize = new Vector2(190, 420), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		list.AddChild(listScroll);
		_entityList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_entityList.Reparent(listScroll);
		_settlementDetails = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		people.AddChild(_settlementDetails);
		foreach (var name in new[] { "InspectorHeading", "SquadHeading", "SquadMembership", "SquadTarget", "SquadOrders", "ScheduleHeading", "ScheduleHours", "Inspector", "RemoveSelected" })
			root.GetNode<Control>(name).Reparent(_settlementDetails);
		_settlementTabs.TabChanged += _ => ShowSettlementTab();
		foreach (var name in new[] { "Save", "Load", "Reset" }) root.GetNode<Control>(name).Hide();
		ShowSettlementTab();
		ApplyMenuTheme(_controlsPanel);
	}

	private void AddPlannedSettingsTab(string title, string explanation)
	{
		AddTextTab(_settingsTabs, title + " (Planned)").Text = explanation;
		var index = _settingsTabs.GetTabCount() - 1;
		_settingsTabs.SetTabDisabled(index, true);
		_settingsTabs.GetTabBar().SetTabTooltip(index, $"Planned: {explanation}");
	}

	private void CraftPlayerAxe()
	{
		var result = new PlayerToolService(_world).CraftAxe();
		ShowActionFeedback(result.Message);
		UpdatePlayerMenuContents();
	}

	private void EquipPlayerAxe()
	{
		var result = new PlayerToolService(_world).EquipAxe();
		ShowActionFeedback(result.Message);
		UpdatePlayerMenuContents();
	}

	private VBoxContainer CreateMenuPanel(string title, out Control panel)
	{
		panel = new PanelContainer { Name = title.Replace(" / ", ""), AnchorLeft = 0.12f, AnchorRight = 0.88f, AnchorBottom = 1,
			OffsetLeft = 20, OffsetTop = 92, OffsetRight = -20, OffsetBottom = -20 };
		_menuHud.AddChild(panel);
		ApplyMenuTheme(panel);
		var margin = new MarginContainer();
		foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", 16);
		panel.AddChild(margin);
		var content = new VBoxContainer();
		margin.AddChild(content);
		var header = new HBoxContainer();
		content.AddChild(header);
		var titleLabel = new Label { Text = title.ToUpperInvariant(), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		titleLabel.AddThemeFontSizeOverride("font_size", 23);
		header.AddChild(titleLabel);
		AddMenuButton(header, "Close", ClosePlayerMenus);
		return content;
	}

	private static void ApplyMenuTheme(Control panel)
	{
		var theme = new Theme { DefaultFontSize = 16 };
		theme.SetColor("font_color", "Label", new Color(0.88f, 0.92f, 0.97f));
		var background = new StyleBoxFlat { BgColor = new Color(0.055f, 0.075f, 0.11f, 0.98f), BorderColor = new Color(0.22f, 0.32f, 0.43f),
			BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 };
		theme.SetStylebox("panel", "PanelContainer", background);
		theme.SetStylebox("panel", "TabContainer", new StyleBoxFlat { BgColor = new Color(0.055f, 0.075f, 0.11f) });
		panel.Theme = theme;
	}

	private static VBoxContainer AddScrollTab(TabContainer tabs, string title)
	{
		var scroll = new ScrollContainer { Name = title, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		tabs.AddChild(scroll);
		var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		scroll.AddChild(content);
		return content;
	}

	private static Label AddTextTab(TabContainer tabs, string title)
	{
		var content = AddScrollTab(tabs, title);
		var label = MenuLabel();
		content.AddChild(label);
		return label;
	}

	private static Label MenuLabel(string text = "") => new()
	{
		Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart,
		SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore
	};

	private static Button AddMenuButton(Node parent, string text, Action action)
	{
		var button = new Button { Text = text };
		button.Pressed += action;
		parent.AddChild(button);
		return button;
	}

	private void ShowSettlementTab()
	{
		var root = GetNode<VBoxContainer>("Controls/Panel/Scroll/Margin/VBox");
		foreach (var node in root.GetChildren().OfType<Control>())
		{
			var name = node.Name.ToString();
			node.Visible = name is "MenuTitle" or "MenuTabs"
				|| (_settlementTabs.CurrentTab == 0 && name is "Status" or "Pause" or "OneX" or "FourX" or "Construction")
				|| (_settlementTabs.CurrentTab is 1 or 2 && name == "PeopleDetails");
		}
		foreach (var node in _settlementDetails.GetChildren().OfType<Control>())
			node.Visible = node.Name.ToString() is "InspectorHeading" or "Inspector"
				|| (_settlementTabs.CurrentTab is 1 or 2 && node.Name.ToString() == "IndividualOrders")
				|| (_settlementTabs.CurrentTab == 1 && node.Name.ToString() is "ScheduleHeading" or "ScheduleHours" or "RemoveSelected")
				|| (_settlementTabs.CurrentTab == 2 && node.Name.ToString() is "SquadHeading" or "SquadMembership" or "SquadTarget" or "SquadOrders");
		GetNode<ScrollContainer>("Controls/Panel/Scroll").ScrollVertical = 0;
	}

	private void ClosePlayerMenus()
	{
		_captureBinding = null;
		_menuState.Close();
		ApplyMenuState();
	}

	private void ApplyMenuState()
	{
		CloseWorkstation();
		CloseFarm();
		CloseConstruction();
		CancelHutPlacement();
		CancelGameplayActions();
		_captureBinding = null;
		_controls.Visible = _sessionActive && _menuState.ActiveMenu == PlayerMenu.Settlement;
		_inventoryPanel.Visible = _sessionActive && _menuState.ActiveMenu == PlayerMenu.Inventory;
		_settingsPanel.Visible = _sessionActive && _menuState.ActiveMenu == PlayerMenu.Settings;
		_menuHud.Visible = _sessionActive;
		_playerCharacter.SetGameplayInputEnabled(_sessionActive && _menuState.ActiveMenu is null && !_clock.IsPaused);
		foreach (var action in ControllerActions.All) Input.ActionRelease(action);
		GetViewport().GuiReleaseFocus();
		UpdatePlayerMenuContents();
		UpdateWorldSelection();
	}

	public override void _Input(InputEvent input)
	{
		if (!_sessionActive) return;
		if (input is InputEventKey { Pressed: false } or InputEventKey { Echo: true }
			or InputEventMouseButton { Pressed: false } or InputEventJoypadButton { Pressed: false }) return;
		if (WorkstationPanelOpen || FarmPanelOpen || ConstructionPanelOpen || PlacementPanelOpen)
		{
			if (input.IsActionPressed(ControllerActions.CloseMenu))
			{
				if (WorkstationPanelOpen) CloseWorkstation();
				if (FarmPanelOpen) CloseFarm();
				if (ConstructionPanelOpen) CloseConstruction();
				if (PlacementPanelOpen) CancelHutPlacement();
				GetViewport().SetInputAsHandled();
			}
			return;
		}
		if (_captureBinding is { } capture)
		{
			if (input is InputEventKey key && (key.PhysicalKeycode == Key.Escape || key.Keycode == Key.Escape))
			{
				_captureBinding = null;
				_bindingStatus.Text = "Mapping cancelled.";
			}
			else if (input is InputEventKey or InputEventMouseButton or InputEventJoypadButton)
			{
				if (_buttonBindings.TryBind(capture.Action, capture.Controller, input, out var reason)) _captureBinding = null;
				_bindingStatus.Text = reason;
				RefreshMappingButtons();
			}
			GetViewport().SetInputAsHandled();
			return;
		}
		if (input.IsActionPressed(ControllerActions.CloseMenu))
		{
			if (_menuState.ActiveMenu is null && (_selectedTarget is not null || _targetLocked))
			{
				ClearTargetLock();
				GetViewport().SetInputAsHandled();
				return;
			}
			if (_menuState.ActiveMenu is null) ToggleMenu(PlayerMenu.Settings);
			else ClosePlayerMenus();
		}
		else if (input.IsActionPressed(ControllerActions.Inventory)) ToggleMenu(PlayerMenu.Inventory);
		else if (input.IsActionPressed(ControllerActions.Settlement)) ToggleMenu(PlayerMenu.Settlement);
		else if (input.IsActionPressed(ControllerActions.Settings)) ToggleMenu(PlayerMenu.Settings);
		else return;
		GetViewport().SetInputAsHandled();
	}

	private void RefreshMappingButtons()
	{
		foreach (var (key, button) in _mappingButtons)
		{
			var binding = _buttonBindings.Get(key.Action);
			button.Text = PlayerButtonBindings.Label(key.Controller ? binding.Controller : binding.Desktop);
		}
		UpdatePlayerMenuContents();
	}

	private void UpdatePlayerMenuContents()
	{
		if (_menuHint is null) return;
		string Binding(string action) => PlayerButtonBindings.Label(_buttonBindings.Get(action).Desktop);
		_menuHint.Text = $"{Binding(ControllerActions.Inventory)} Inventory  |  {Binding(ControllerActions.Settlement)} Settlement  |  {Binding(ControllerActions.Settings)} Save / Settings  |  {Binding(ControllerActions.CycleTarget)} Target lock  |  Esc close / menu";
		_settingsStatus.Text = $"{(_clock.IsPaused ? "Paused" : "Running")} · {_clock.SimulationTime:0.0}s · {(int)_clock.Speed}x\n{_saveLoadStatus}";
		if (_world.PlayerEntityId is not { } id || !_world.TryGetEntity(id, out var player) || player is null)
		{
			_characterIdentity.Text = "No player character in this save.";
			_characterStatus.Text = _characterAttributes.Text = _equipmentSummary.Text = _carriedItems.Text = _otherCharacterInfo.Text = "Unavailable.";
			return;
		}
		var profile = _world.PlayerProfile!;
		_playerCharacter.SetAxeEquipped(string.Equals(player.EquippedTool, ItemIds.Axe, StringComparison.Ordinal));
		_characterIdentity.Text = $"{profile.Name} · {profile.Height} · {profile.BodyType} · {profile.Sex}";
		var stats = player.BaseStats;
		_characterStatus.Text = $"STATUS\nSatiety: {player.Satiety:0.0} / 100\nCondition: {(player.Health.IsDead ? "Dead" : player.Health.IsIncapacitated ? "Incapacitated" : "Conscious")}\n\nBODY HEALTH\n" +
			string.Join("\n", player.Health.Parts.Select(part => $"{part.Name}: {part.CurrentHealth:0.#} / {part.MaximumHealth:0.#}"));
		_characterAttributes.Text = $"BASE ATTRIBUTES\n\nStrength: {stats.Strength:0.#}\nEndurance: {stats.Endurance:0.#}\nDefense: {stats.Defense:0.#}\nPerception: {stats.Perception:0.#}\nMovement speed: {stats.MovementSpeed:0.#}\nMaximum stamina: {stats.MaxStamina:0.#}";
		_equipmentSummary.Text = $"EQUIPMENT\n\nTool: {(player.EquippedTool ?? "None")}\nLeft hand: {(_playerCharacter.LeftShieldEquipped ? "Shield" : "Empty")}\nRight hand: {(_playerCharacter.RightShieldEquipped ? "Shield" : "Empty")}\n\nUnarmed attacks are available in empty hands.\n\nPLANNED SLOTS";
		var items = player.Inventory.Where(item => _itemCategory.Selected switch
		{
			1 => ItemIds.IsEdibleFood(item.Key), 2 => item.Key == ItemIds.Wood, 3 => item.Key == ItemIds.Coin, _ => true
		}).OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
		_carriedItems.Text = "CARRIED ITEMS\n" + (items.Length == 0 ? (player.Inventory.Count == 0 ? "Your inventory is empty." : "No items in this category.") :
			string.Join("\n", items.Select(item => $"{char.ToUpperInvariant(item.Key[0])}{item.Key[1..]}   × {item.Value}"))) +
			$"\n\nTotal units: {player.Inventory.Values.Sum(count => (long)count)}\nItems shown here are carried by your character. Ground and settlement stockpiles are separate.";
		_otherCharacterInfo.Text = "SKILLS\n" + string.Join("\n", player.Skills.Skills.Select(skill => $"{skill.Definition.Id}: level {skill.Level} · XP {skill.Experience:0.#}")) +
			$"\n\nWORLD\nPosition: {player.Position.X:0.0}, {player.Position.Z:0.0}\nFaction: {player.AllegianceFactionId ?? "None"}";
	}
}
