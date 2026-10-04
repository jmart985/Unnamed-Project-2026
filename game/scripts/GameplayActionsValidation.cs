using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private async void RunWorkbenchValidation()
    {
        try
        {
            StartSession(PlayerCharacterProfile.Create("Workbench validation"), 42, "Town workbench validation.");
            SetProcess(false); _playerCharacter.SetPhysicsProcess(false);
            Require(_world.Workstations.Single().Id == "workbench-1"
                && _world.Workstations.Single().Position == NewGameSession.TownWorkbenchPosition,
                "reuse existing starter workstation placement");
            Require(GetNodeOrNull<Node3D>("Obstacle") is null && _workbench.IsVisibleInTree()
                && _workbench.GetNode<Label3D>("WorkbenchLabel").Text == "Workbench", "visible identifiable bench replaces old cube");
            var parts = _workbench.FindChildren("*", "MeshInstance3D", true, false).Count;
            Require(parts > 4 && _workbench.Mesh.GetAabb().Size.Y < 0.3f, "tabletop, supports and work tools rendered");
            Require(IsBlockedCell(-1, 1, -1, 1), "existing navigation footprint retained");
            var target = new WorldTarget(TargetKind.Entity, "validation-target");
            Require(!TargetLineClear(new(-2, 1.05f, 0), new(2, 1.05f, 0), target, []), "tabletop obstructs real sight/strike ray");
            Require(TargetLineClear(new(-2, 0.55f, 0), new(2, 0.55f, 0), target, []), "empty space below bench is not the old solid obstacle bounds");
            _playerCharacter.GlobalPosition = new Vector3(-2.5f, 0, -3); SyncPlayerPosition();
            foreach (var view in _entityViews.Values) view.Visible = false;
            var camera = GetNode<Camera3D>("Camera3D"); camera.Current = true;
            camera.GlobalPosition = new Vector3(4.2f, 3.2f, 4.6f); camera.LookAt(new Vector3(0, 0.7f, 0));
            await PhysicsFrames(5);
            if (DisplayServer.GetName() != "headless")
            {
                var directory = ProjectSettings.GlobalizePath("res://../docs/evidence/workbench");
                Directory.CreateDirectory(directory);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, "town-center.png")) == Error.Ok, "actual rendered workbench evidence");
            }
            ResetScenario(); SetProcess(false); _playerCharacter.SetPhysicsProcess(false);
            ResetScenario(); SetProcess(false); _playerCharacter.SetPhysicsProcess(false);
            Require(_workbench.FindChildren("*", "MeshInstance3D", true, false).Count == parts
                && _world.Workstations.Count == 1, "repeated reset does not duplicate props or workstations");
            _saveSlotPath = ProjectSettings.GlobalizePath("user://workbench-validation-save.json");
            SaveScenario(); LoadScenario();
            Require(_workbench.IsVisibleInTree() && _workbench.FindChildren("*", "MeshInstance3D", true, false).Count == parts,
                "static town landmark remains visible after load without duplicate geometry");
            GD.Print("WORKBENCH VALIDATION PASSED: town-center placement, visible labeled bench, actual mesh occlusion, existing navigation footprint, repeated reset/load and rendered evidence.");
            GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError($"WORKBENCH VALIDATION FAILED: {exception}"); GetTree().Quit(1); }
    }

    private async void RunSocialFeedbackValidation()
    {
        try
        {
            StartSession(PlayerCharacterProfile.Create("Social feedback"), 42, "Social feedback validation.");
            SetProcess(false);
            _playerCharacter.SetPhysicsProcess(false);
            _playerCharacter.GlobalPosition = new Vector3(0, 0, -6);
            _playerCharacter.Rotation = Vector3.Zero; SyncPlayerPosition();
            var player = _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId);
            var cow = _world.Entities.Single(entity => entity.SpeciesId == "cow");
            var human = _world.Entities.First(entity => entity.SpeciesId == "human" && entity.Id != player.Id);
            foreach (var (id, view) in _entityViews) view.Visible = id == cow.Id || id == human.Id;
            foreach (var source in _world.FoodSources.ToArray()) _world.TryRemoveFoodSource(source.Id);
            foreach (var source in _world.ResourcePiles.ToArray()) _world.TryRemoveResourcePile(source.Id);
            foreach (var source in _world.Stockpiles.ToArray()) _world.TryRemoveStockpile(source.Id);
            ClearResourceMarkers(); RebuildResourceMarkersFromWorld();
            _world.TrySetEntityPosition(cow.Id, new(0, 0, -7.8f));
            _world.TrySetEntityPosition(human.Id, new(4, 0, -7.5f)); SyncEntityViews();
            var camera = GetNode<Camera3D>("Camera3D"); camera.Current = true;
            camera.GlobalPosition = new Vector3(0, 5, 1); camera.LookAt(new Vector3(0, 1, -7));
            await PhysicsFrames(5);
            SelectEntity(cow.Id);
            Require(_targetHud.Text.Contains("Trusted") && _targetHud.Text.Contains("Pet available"), "domestic baseline is Trusted before any interaction");
            _world.ApplyRelationshipAdjustment(cow.Id, player.Id, new(0, -1, 0, 0));
            CancelGameplayActions(); UpdateWorldSelection();
            var lockedColor = ((StandardMaterial3D)_targetRing.MaterialOverride).AlbedoColor;
            var saved = WorldSnapshotSerializer.Serialize(_world, 0);
            SendMenuKey(Key.F);
            Require(_actionHud.Text.Contains("Pet unavailable") && _socialPopups.Count == 0
                && saved == WorldSnapshotSerializer.Serialize(_world, 0), "untrusted pet rejects without state change or fake gain");
            await CaptureSocialFeedback("pet-locked.png");
            var foodStack = new ResourceContainer("social food fixture", 2); foodStack.TryAdd(ItemIds.Plant, 2);
            Require(ResourceTransferService.TryPickUp(foodStack, player, ItemIds.Plant, 2) == ResourceMutationResult.Success, "carried food fixture");
            SendMenuKey(Key.F);
            Require(_world.TryGetRelationship(cow.Id, player.Id, out var bond) && bond!.Trust == 50,
                "real F feeding crosses 49 to 50");
            Require(player.Inventory[ItemIds.Plant] == 1 && _socialPopups.Any(popup => popup.EntityId == cow.Id
                && popup.Label.Visible && popup.Label.Text.Contains("Trust +1") && popup.Label.Text.Contains("Familiarity +1")
                && popup.Label.Text.Contains("Loyalty +1") && !popup.Label.Text.Contains("Fear")), "feeding shows actual unit gains and omits clamped fear");
            Require(_targetHud.Text.Contains("Trusted") && !_targetHud.Text.Contains("Familiarity")
                && !_targetHud.Text.Contains("Trust 50") && ((StandardMaterial3D)_targetRing.MaterialOverride).AlbedoColor != lockedColor,
                "Trusted ring changes and persistent numeric totals stay hidden");
            await CaptureSocialFeedback("trusted-feed.png");
            CancelGameplayActions();
            SendMenuKey(Key.F);
            Require(_actionHud.Text.Contains("petted") && bond!.Trust == 51 && player.Inventory[ItemIds.Plant] == 1,
                "full Trusted creature can be petted even while carrying food");
            Require(_entityViews[cow.Id].HasGreetingAttention, "pet response preserves sustained attention");
            _playerCharacter.SetPhysicsProcess(true);
            await PhysicsFrames(5);
            var hp = cow.Health.Parts.Sum(part => part.CurrentHealth);
            SendMenuMouse(MouseButton.Left); await PhysicsFrames(30);
            SendMenuMouse(MouseButton.Right); await PhysicsFrames(30);
            Require(cow.Health.Parts.Sum(part => part.CurrentHealth) == hp - 20 && bond!.Trust == 49,
                $"actual attacks land and remove trust at threshold: {_lastActionMessage}; trust {bond!.Trust}");
            UpdateWorldSelection();
            Require(_targetHud.Text.Contains("Pet unavailable") && ((StandardMaterial3D)_targetRing.MaterialOverride).AlbedoColor == lockedColor,
                "loss immediately removes Trusted color and pet permission");
            Require(_socialPopups.Any(popup => popup.EntityId == cow.Id && popup.Label.Visible
                && popup.Label.Text.Contains("Trust -1") && popup.Label.Text.Contains("Fear +1") && popup.Label.Text.Contains("Loyalty -1")),
                "harm shows creature losses and fear gain above the recipient");
            await CaptureSocialFeedback("trust-loss.png");
            CancelGameplayActions();
            Require(ResourceTransferService.TryDeliver(player, foodStack, ItemIds.Plant, 1) == ResourceMutationResult.Success, "clear carried food for pet-only test");
            SendMenuKey(Key.F);
            Require(_actionHud.Text.Contains("Pet unavailable") && bond.Trust == 49 && _socialPopups.Count == 0, "pet is removed after harm");
            _playerCharacter.SetPhysicsProcess(false);
            _world.TrySetEntityPosition(human.Id, new(1.5f, 0, -6)); SyncEntityViews(); SelectEntity(human.Id);
            SendMenuKey(Key.G);
            Require(_socialPopups.Any(popup => popup.EntityId == human.Id && popup.Label.Visible && popup.Label.Text == "Familiarity +1"),
                "human greeting feedback belongs to NPC rather than player");
            Require(!_world.TryGetRelationship(player.Id, human.Id, out _), "player outgoing relationship is not fabricated");
            await CaptureSocialFeedback("npc-greeting.png");
            CancelGameplayActions();
            _world.ApplyRelationshipAdjustment(human.Id, player.Id, new(0, 1, 1, 1));
            CancelGameplayActions();
            _world.ApplyRelationshipAdjustment(human.Id, player.Id, new(-1, -1, -1, -1));
            Require(_socialPopups.Any(popup => popup.Label.Text.Contains("Fear -1") && popup.Label.Text.Contains("Loyalty -1")),
                "NPC relationship losses show applied unit changes");
            await CaptureSocialFeedback("npc-relationship-loss.png");
            CancelGameplayActions();
            _world.DefineFaction("settlers");
            Require(_world.ApplyFactionStandingEvent(human.Id, "settlers", true) == FactionReputationResult.Applied, "unit faction gain");
            Require(_world.ApplyFactionStandingEvent(human.Id, "settlers", false) == FactionReputationResult.Applied, "unit faction loss");
            Require(_socialPopups.Any(popup => popup.Label.Text == "settlers standing +1")
                && _socialPopups.Any(popup => popup.Label.Text == "settlers standing -1"),
                "NPC relationship and faction losses show honest signed changes");
            await CaptureSocialFeedback("npc-standing.png");
            UpdateGameplayFeedback(3.1);
            Require(_socialPopups.Count == 0, "feedback expires without keeping numeric totals on screen");
            SendMenuKey(Key.G); ToggleMenu(PlayerMenu.Inventory);
            Require(_socialPopups.Count == 0, "menus clear social feedback");
            ClosePlayerMenus(); UpdateInspector();
            Require(!_inspectorLabel.Text.Contains("Familiarity ") && !_inspectorLabel.Text.Contains("Trust "), "settlement inspector hides relationship totals");
            _saveSlotPath = ProjectSettings.GlobalizePath("user://social-feedback-validation-save.json");
            SaveScenario(); LoadScenario();
            Require(_world.TryGetRelationship(cow.Id, player.Id, out var loaded) && loaded!.Trust == 49
                && _socialPopups.Count == 0, "save/load preserves betrayed trust without reseeding or replaying feedback");
            GD.Print("SOCIAL FEEDBACK VALIDATION PASSED: real feed/pet/attacks, Trusted ring and reversible permission, directed NPC feedback, all relationship dimensions, faction unit events, expiry/menu isolation and save/load.");
            GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError($"SOCIAL FEEDBACK VALIDATION FAILED: {exception}"); GetTree().Quit(1); }
    }

    private async Task CaptureSocialFeedback(string filename)
    {
        if (DisplayServer.GetName() == "headless") return;
        var directory = ProjectSettings.GlobalizePath("res://../docs/evidence/social-feedback");
        Directory.CreateDirectory(directory);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, filename)) == Error.Ok, "rendered social feedback evidence");
    }

    private async void RunGameplayActionsValidation()
    {
        try
        {
            StartSession(PlayerCharacterProfile.Create("Gameplay refinement"), 42, "Gameplay refinement validation.");
            SetProcess(false);
            _playerCharacter.GlobalPosition = new Vector3(0, 0, -6);
            _playerCharacter.Rotation = Vector3.Zero;
            SyncPlayerPosition();
            var player = _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId);
            var human = _world.Entities.First(entity => entity.Id != player.Id && entity.SpeciesId == "human");
            var cow = _world.Entities.Single(entity => entity.SpeciesId == "cow");
            var wolf = _world.Entities.Single(entity => entity.SpeciesId == "wolf");
            foreach (var (id, view) in _entityViews) view.Visible = id == human.Id || id == cow.Id || id == wolf.Id;
            _world.TrySetEntityPosition(human.Id, new(0, 0, -7.5f));
            _world.TrySetEntityPosition(cow.Id, new(-1.5f, 0, -6));
            _world.TrySetEntityPosition(wolf.Id, new(3, 0, -9));
            foreach (var source in _world.FoodSources.ToArray()) _world.TryRemoveFoodSource(source.Id);
            foreach (var source in _world.ResourcePiles.ToArray()) _world.TryRemoveResourcePile(source.Id);
            foreach (var source in _world.Stockpiles.ToArray()) _world.TryRemoveStockpile(source.Id);
            var food = new ResourceContainer("validation food", 20); food.TryAdd(ItemIds.Food, 8);
            var stock = new ResourceContainer("validation stock", 20); stock.TryAdd(ItemIds.Wood, 5);
            _world.AddFoodSource("refinement-food", new(1.3f, 0, -6), food);
            _world.AddStockpile("refinement-stock", human.Position, stock);
            ClearResourceMarkers(); RebuildResourceMarkersFromWorld(); SyncEntityViews();
            var camera = GetNode<Camera3D>("Camera3D"); camera.Current = true;
            camera.GlobalPosition = new Vector3(0, 5, 1); camera.LookAt(new Vector3(0, 0.8f, -7));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var humanTarget = new WorldTarget(TargetKind.Entity, human.Id);
            Require(VisibleWorldTargets().Any(target => target.Target == humanTarget), "human overlapping stockpile selectable via exposed body meshes");
            var seen = new HashSet<WorldTarget>();
            for (var index = 0; index < VisibleWorldTargets().Count; index++) { SendMenuKey(Key.T); seen.Add(_selectedTarget!); }
            Require(seen.Contains(humanTarget), "real T cycling reaches overlapping human");
            SelectEntity(human.Id); UpdateStatus(); await CaptureGameplayRefinement("overlap-selection.png");
            var blocker = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(4, 4, 1) }, Position = new Vector3(0, 2, -7) };
            _scenery!.AddChild(blocker);
            Require(!VisibleWorldTargets().Any(target => target.Target == humanTarget), "fully obstructed human excluded");
            blocker.Visible = false;

            _selectedTarget = null; UpdateStatus();
            var satiety = player.Satiety;
            SendMenuKey(Key.E);
            Require(player.Inventory.GetValueOrDefault(ItemIds.Food) == 1 && food.Counts[ItemIds.Food] == 7
                && player.Satiety == satiety, "real short E collects nearest visible resource without selection or consumption");
            Require(_actionHud.Visible && _actionHud.Text.Contains("Collected 1 food") && _menuState.ActiveMenu is null,
                "pickup feedback visible in gameplay");
            _selectedTarget = new(TargetKind.Food, "refinement-food");
            var beforeHold = food.Counts[ItemIds.Food];
            GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.E, Keycode = Key.E, Pressed = true });
            AdvanceCollectUse(0.3); UpdateGameplayFeedback(0);
            Require(_actionHud.Text.Contains("50%"), "visible hold progress");
            AdvanceCollectUse(0.3); AdvanceCollectUse(3);
            GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.E, Keycode = Key.E, Pressed = false });
            Require(player.Inventory.GetValueOrDefault(ItemIds.Food) == 0 && food.Counts[ItemIds.Food] == beforeHold
                && player.Satiety == Math.Min(100, satiety + player.Needs.NutritionPerUnit), "hold consumes once from carried food and does not pick up");
            SendMenuKey(Key.E);
            foreach (var menu in Enum.GetValues<PlayerMenu>())
            {
                GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.E, Keycode = Key.E, Pressed = true });
                ToggleMenu(menu); AdvanceCollectUse(1); ClosePlayerMenus();
                GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.E, Keycode = Key.E, Pressed = false });
                Require(player.Inventory.GetValueOrDefault(ItemIds.Food) == 1 && !_collectUse.IsPressed, "menu cancels pending press, reopening does not commit");
            }
            BeginCollectUse(); TogglePause(); AdvanceCollectUse(1); TogglePause(); CompleteCollectUse();
            Require(player.Inventory.GetValueOrDefault(ItemIds.Food) == 1, "pause cancels pending collect/use");
            BeginCollectUse(); GetWindow().EmitSignal(Window.SignalName.FocusExited); CompleteCollectUse();
            Require(player.Inventory.GetValueOrDefault(ItemIds.Food) == 1 && !_collectUse.IsPressed, "focus loss cancels pending collect/use");
            _buttonBindings.Path = ProjectSettings.GlobalizePath("user://refinement-validation-settings.json");
            Require(_buttonBindings.TryBind(ControllerActions.Collect, false, new InputEventKey { PhysicalKeycode = Key.K, Pressed = true }, out _), "collect rebind persists");
            SendMenuKey(Key.K);
            Require(player.Inventory[ItemIds.Food] == 2, "remapped key still collects");
            GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = JoyButton.X, Pressed = true });
            GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = JoyButton.X, Pressed = false });
            Require(player.Inventory[ItemIds.Food] == 3, "controller short press collects");
            player.Needs.Advance(200);
            var carriedBeforeRealHold = player.Inventory[ItemIds.Food];
            var groundBeforeRealHold = food.Counts[ItemIds.Food];
            GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = JoyButton.X, Pressed = true });
            SetProcess(true);
            await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
            GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = JoyButton.X, Pressed = false });
            SetProcess(false);
            Require(player.Inventory[ItemIds.Food] == carriedBeforeRealHold - 1 && food.Counts[ItemIds.Food] == groundBeforeRealHold
                && player.Satiety > 95, "real elapsed controller hold dispatches through Main process once");
            _world.TrySetEntityPosition(cow.Id, new(-1.5f, 0, -6));
            _world.TrySetEntityPosition(human.Id, new(0, 0, -7.5f));
            // Freeze the player during interaction assertions/screenshots so overlapping
            // fixture colliders cannot push the actor out of the deliberate 2 m reach.
            _playerCharacter.SetPhysicsProcess(false);
            _playerCharacter.GlobalPosition = new Vector3(0, 0, -6); SyncPlayerPosition();
            SyncEntityViews();
            _buttonBindings.RestoreDefaults(out _);

            SelectEntity(cow.Id);
            var plantStack = new ResourceContainer("feed fixture", 3); plantStack.TryAdd(ItemIds.Plant, 3);
            Require(ResourceTransferService.TryPickUp(plantStack, player, ItemIds.Plant, 3) == ResourceMutationResult.Success, "feed stack fixture pickup");
            var cowSatiety = cow.Satiety;
            SendMenuKey(Key.F);
            Require(_actionHud.Text.StartsWith("Fed plant", StringComparison.Ordinal) && _targetResponse.Visible
                && player.Inventory[ItemIds.Plant] == 2
                && cow.Satiety == Math.Min(100, cowSatiety + cow.Needs.NutritionPerUnit), $"F feeds exactly one from a carried stack and shows acknowledgment: {_lastActionMessage}; carried {player.Inventory[ItemIds.Plant]}; satiety {cow.Satiety}; response {_targetResponse.Visible}");
            await CaptureGameplayRefinement("feed.png");
            SendMenuKey(Key.F);
            Require(_actionHud.Text.Contains("petted") && player.Inventory[ItemIds.Plant] == 2, "full Trusted target can be petted without item waste");
            Require(ResourceTransferService.TryDeliver(player, plantStack, ItemIds.Plant, 2) == ResourceMutationResult.Success, "feed stack fixture removal");
            SendMenuKey(Key.F);
            Require(_actionHud.Text.Contains("petted") && _targetResponse.Visible
                && _world.TryGetRelationship(cow.Id, player.Id, out var petBond) && petBond!.Trust == 53, "F pets with visible directed response when no compatible food is carried");
            var captured = _interactionResolver.Resolve(new(player.Id, cow.Id, InteractionType.Physical, "capture"));
            Require(captured.Succeeded && cow.CaptivityState == CreatureCaptivityState.Captured, "live capture resolver uses World lifecycle");
            var given = new ResourceContainer("give descriptor", 1); given.TryAdd(ItemIds.Food, 1);
            var foodBeforeGive = player.Inventory[ItemIds.Food];
            var give = _interactionResolver.Resolve(new(player.Id, human.Id, InteractionType.Physical, "give", given));
            Require(give.Succeeded && player.Inventory[ItemIds.Food] == foodBeforeGive - 1 && human.Inventory[ItemIds.Food] == 1,
                $"live give resolver conserves actor and recipient inventory: {give.Reason}");
            SelectEntity(human.Id); SendMenuKey(Key.G);
            Require(_actionHud.Text.Contains("Successfully performed greet") && _targetResponse.Visible, "G greets humans visibly");
            var humanView = _entityViews[human.Id];
            humanView._Process(3);
            Require(humanView.VisualForward.Dot((_playerCharacter.GlobalPosition - humanView.GlobalPosition).Normalized()) > 0.99f,
                "human continues facing greeter after acknowledgment");
            await CaptureGameplayRefinement("human-greeting.png");
            humanView._Process(60);
            Require(humanView.HasGreetingAttention, "greeting attention has no elapsed-time timeout");
            var playerBeforeAttentionRange = _playerCharacter.GlobalPosition;
            _playerCharacter.GlobalPosition = humanView.GlobalPosition + new Vector3(2, 0, 0);
            humanView._Process(0.1);
            Require(humanView.HasGreetingAttention, "attention remains at exactly 2 m separation");
            _playerCharacter.GlobalPosition = humanView.GlobalPosition + new Vector3(2.01f, 0, 0);
            humanView._Process(0.1);
            Require(!humanView.HasGreetingAttention, "attention stops beyond 2 m separation");
            _playerCharacter.GlobalPosition = playerBeforeAttentionRange; SyncPlayerPosition();
            SendMenuKey(Key.G);
            Require(humanView.HasGreetingAttention, "new nearby greeting restores attention");
            human.Actions.Cancel();
            Require(human.Actions.TryStart(new ActionCandidate("attention-override", ActionKind.Eat)) == ActionStartResult.Started,
                "new task fixture starts");
            humanView.Sync(_world);
            Require(!humanView.HasGreetingAttention, "new task overrides greeting attention even before movement");
            human.Actions.Cancel();
            ToggleMenu(PlayerMenu.Inventory); SendMenuKey(Key.G); SendMenuKey(Key.F); ClosePlayerMenus();
            Require(player.Inventory[ItemIds.Food] == foodBeforeGive - 1, "menus isolate interactions");
            SelectEntity(cow.Id); SendMenuKey(Key.G);
            Require(_actionHud.Text.StartsWith("Greeted", StringComparison.Ordinal) && _targetResponse.Visible
                && _targetHud.Text.Contains("Trusted") && !_targetHud.Text.Contains("Familiarity"), "G exposes recipient bond without numeric relationship totals");
            var greeterDirection = (_playerCharacter.GlobalPosition - _entityViews[cow.Id].GlobalPosition).Normalized();
            Require(_entityViews[cow.Id].VisualForward.Dot(greeterDirection) > 0.99f, "greeted cow faces greeter");
            _entityViews[cow.Id]._Process(0.1);
            Require(_entityViews[cow.Id].VisualForward.Dot(greeterDirection) > 0.99f, "greeting turn survives gait update");
            await PhysicsFrames(15);
            _entityViews[cow.Id]._Process(3);
            Require(_entityViews[cow.Id].VisualForward.Dot(greeterDirection) > 0.99f, "idle creature remains facing greeter after acknowledgment duration");
            var priorPlayerPosition = _playerCharacter.GlobalPosition;
            _playerCharacter.GlobalPosition += new Vector3(0, 0, 0.5f);
            _entityViews[cow.Id]._Process(0.1);
            Require(_entityViews[cow.Id].VisualForward.Dot((_playerCharacter.GlobalPosition - _entityViews[cow.Id].GlobalPosition).Normalized()) > 0.99f,
                "idle attention follows the live greeter position after greeting");
            _playerCharacter.GlobalPosition = priorPlayerPosition;
            _entityViews[cow.Id]._Process(0.1);
            await CaptureGameplayRefinement("greeting.png");
            SelectEntity(wolf.Id); SendMenuKey(Key.G);
            Require(_actionHud.Text.Contains("does not accept") && !_targetResponse.Visible, "unsupported greeting visible without false acknowledgment");
            _world.TrySetEntityPosition(cow.Id, new(-10, 0, -6)); SyncEntityViews(); SelectEntity(cow.Id); SendMenuKey(Key.G);
            Require(_actionHud.Text.Contains("Move closer"), "distant greeting visible rejection");
            _world.TrySetEntityPosition(cow.Id, new(-1.5f, 0, -6)); SyncEntityViews();

            // Remove low storage only for strike tests: a selection can be visible while a strike is physically blocked.
            _world.TryRemoveStockpile("refinement-stock"); ClearResourceMarkers(); RebuildResourceMarkersFromWorld();
            _playerCharacter.SetPhysicsProcess(true);
            await PhysicsFrames(25);
            SelectEntity(human.Id);
            var hp = human.Health.Parts.Sum(part => part.CurrentHealth);
            SendMenuMouse(MouseButton.Left); SelectEntity(cow.Id);
            await PhysicsFrames(30);
            Require(human.Health.Parts.Sum(part => part.CurrentHealth) == hp - 10, "actual swing hits captured target once despite selection changing during windup");
            Require(_actionHud.Text.Contains("Hit human Torso") && _world.TryGetRelationship(human.Id, player.Id, out _), "hit feedback and harmed relationship apply");
            SelectEntity(human.Id); UpdateStatus(); await CaptureGameplayRefinement("targeted-hit.png");
            hp -= 10;
            blocker.Visible = true; SendMenuMouse(MouseButton.Right); await PhysicsFrames(30);
            Require(human.Health.Parts.Sum(part => part.CurrentHealth) == hp && _actionHud.Text.Contains("blocked"), "blocked target strike does not damage");
            blocker.Visible = false;
            _world.TrySetEntityPosition(human.Id, new(0, 0, -12)); SyncEntityViews();
            SendMenuMouse(MouseButton.Right); await PhysicsFrames(30);
            Require(human.Health.Parts.Sum(part => part.CurrentHealth) == hp && _actionHud.Text.Contains("OutOfRange"), "distant target does not damage");
            _world.TrySetEntityPosition(human.Id, new(0, 0, -7.5f)); SyncEntityViews();
            ToggleMenu(PlayerMenu.Inventory); SendMenuMouse(MouseButton.Left); ClosePlayerMenus(); await PhysicsFrames(30);
            Require(human.Health.Parts.Sum(part => part.CurrentHealth) == hp, "menu blocks strikes");
            SendMenuMouse(MouseButton.Left); TogglePause(); TogglePause(); await PhysicsFrames(30);
            Require(human.Health.Parts.Sum(part => part.CurrentHealth) == hp, "pause cancels windup without a delayed hit");
            SelectEntity(human.Id); SendMenuMouse(MouseButton.Left); _world.TryRemoveEntity(human.Id); await PhysicsFrames(30);
            Require(_actionHud.Text.Contains("unavailable"), "removed windup target rejects safely");
            SelectEntity(cow.Id); player.Health.TryDamage("Torso", 1000);
            var cowHp = cow.Health.Parts.Sum(part => part.CurrentHealth);
            _playerCharacter.Rotation = new Vector3(0, Mathf.Pi / 2, 0);
            SendMenuMouse(MouseButton.Left); await PhysicsFrames(30);
            Require(cow.Health.Parts.Sum(part => part.CurrentHealth) == cowHp, "incapacitated attacker cannot damage");

            _saveSlotPath = ProjectSettings.GlobalizePath("user://refinement-validation-save.json");
            ResetScenario(); SetProcess(false); BeginCollectUse(); SaveScenario(); LoadScenario();
            Require(!_collectUse.IsPressed && _selectedTarget is null && !_targetResponse.Visible, "save/load/reset clear pending input and feedback");
            GD.Print("GAMEPLAY ACTIONS VALIDATION PASSED: actual strike damage and rejection, partial overlap selection, visible F and facing, tap pickup, hold use, keyboard/controller remapping and cancellation.");
            GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError($"GAMEPLAY ACTIONS VALIDATION FAILED: {exception}"); GetTree().Quit(1); }
    }

    private async Task PhysicsFrames(int count)
    {
        for (var index = 0; index < count; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private async Task CaptureGameplayRefinement(string filename)
    {
        if (DisplayServer.GetName() == "headless") return;
        var directory = ProjectSettings.GlobalizePath("res://../docs/evidence/gameplay-refinement");
        Directory.CreateDirectory(directory);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, filename)) == Error.Ok, "rendered refinement evidence");
    }
}
