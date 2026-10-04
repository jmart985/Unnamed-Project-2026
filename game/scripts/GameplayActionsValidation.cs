using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
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
            Require(_actionHud.Text.Contains("already full") && player.Inventory[ItemIds.Plant] == 2, "full target rejects feed without item waste");
            Require(ResourceTransferService.TryDeliver(player, plantStack, ItemIds.Plant, 2) == ResourceMutationResult.Success, "feed stack fixture removal");
            SendMenuKey(Key.F);
            Require(_actionHud.Text.Contains("petted") && _targetResponse.Visible
                && _world.TryGetRelationship(cow.Id, player.Id, out var petBond) && petBond!.Trust == 16, "F pets with visible directed response when no compatible food is carried");
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
                && _targetHud.Text.Contains("Familiarity"), "G exposes supported creature greeting and relationship response without menu");
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
