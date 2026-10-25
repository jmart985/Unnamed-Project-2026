using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private async void RunConstructionValidation()
    {
        try
        {
            StartSession(PlayerCharacterProfile.Create("Construction validation"), 42, "Construction validation.");
            SetProcess(false); _playerCharacter.SetPhysicsProcess(false);
            _playerCharacter.GlobalPosition = new Vector3(-6, 0, -1); _playerCharacter.Rotation = Vector3.Zero; SyncPlayerPosition();
            var player = _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId);
            var playerCamera = _playerCharacter.GetNode<Camera3D>("Camera3D");
            void AimPlayerCameraAt(Vector3 target)
            {
                playerCamera.GlobalPosition = _playerCharacter.GlobalPosition + new Vector3(0, 2.6f, 5.5f);
                playerCamera.LookAt(new Vector3(target.X, 0, target.Z), Vector3.Up);
            }
            bool HasNavigationAt(NavigationMesh navigation, Vector3 point)
            {
                var vertices = navigation.GetVertices();
                for (var polygonIndex = 0; polygonIndex < navigation.GetPolygonCount(); polygonIndex++)
                {
                    var polygon = navigation.GetPolygon(polygonIndex).Select(index => vertices[index]).ToArray();
                    if (point.X > polygon.Min(vertex => vertex.X) && point.X < polygon.Max(vertex => vertex.X)
                        && point.Z > polygon.Min(vertex => vertex.Z) && point.Z < polygon.Max(vertex => vertex.Z))
                        return true;
                }
                return false;
            }
            var sceneryCollider = _scenery!.GetNode<StaticBody3D>("Building_0Collision");
            Require(sceneryCollider.GetChildren().OfType<CollisionShape3D>().Single().Shape is BoxShape3D,
                "solid scenery buildings have matching static collision volumes");
            Require(!ControllerActions.All.Any(action => new[] { Key.Key1, Key.Key2, Key.Key0 }.Any(key =>
                    InputMap.ActionHasEvent(action, new InputEventKey { PhysicalKeycode = key }))),
                "number-row build slots are not globally bound to actions outside Build mode");
            var wood = _world.ResourcePiles.Single().Contents;
            Require(ResourceTransferService.TryPickUp(wood, player, ItemIds.Wood, SettlementConstruction.HutWood)
                == ResourceMutationResult.Success, "construction wood transferred atomically");

            ToggleMenu(PlayerMenu.Settlement);
            PressMenuButton(_controlsPanel, "Build mode");
            Require(PlacementPanelOpen && !_hutPreview.Visible && _playerCharacter.GameplayInputEnabled
                && _placementPanel.MouseFilter == Control.MouseFilterEnum.Ignore && _buildHotbarSlots.Count == 10,
                "Settlement opens a ten-slot non-modal hotbar while player input remains enabled");
            _Input(new InputEventKey { PhysicalKeycode = Key.Key2, Keycode = Key.Key2, Pressed = true });
            Require(_fireplacePreview.Visible && !_hutPreview.Visible, "hotbar slot 2 selects the existing fireplace option");
            _Input(new InputEventJoypadButton { ButtonIndex = JoyButton.DpadRight, Pressed = true });
            Require(_workbenchPreview.Visible, "controller D-pad right cycles to the additional workbench option");
            _Input(new InputEventJoypadButton { ButtonIndex = JoyButton.DpadLeft, Pressed = true });
            Require(_fireplacePreview.Visible, "controller D-pad left cycles back to the fireplace option");
            _Input(new InputEventKey { PhysicalKeycode = Key.Key0, Keycode = Key.Key0, Pressed = true });
            Require(!_fireplacePreview.Visible && !_hutPreview.Visible && _placementMessage.Text.Contains("slot 0 is empty", StringComparison.OrdinalIgnoreCase),
                "hotbar slot 0 is empty and cannot show a build preview");
            var woodBeforeCancel = player.Inventory.GetValueOrDefault(ItemIds.Wood);
            _Input(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            Require(_world.Fireplaces.Count == 0 && _world.ConstructionSites.Count == 0,
                "left click on an empty hotbar slot creates no object");
            _Input(new InputEventKey { PhysicalKeycode = Key.Escape, Keycode = Key.Escape, Pressed = true });
            Require(!PlacementPanelOpen && player.Inventory.GetValueOrDefault(ItemIds.Wood) == woodBeforeCancel,
                "Escape cancels build mode without consuming materials");

            _Input(new InputEventKey { PhysicalKeycode = Key.B, Keycode = Key.B, Pressed = true });
            _Input(new InputEventKey { PhysicalKeycode = Key.Key1, Keycode = Key.Key1, Pressed = true });
            Require(PlacementPanelOpen && _hutPreview.Visible && _placementValid,
                "B opens Build mode and hotbar key 1 selects a valid starter hut preview");
            Require(_targetReticle.Visible, "build mode keeps the centered reticle visible");
            Require(_targetReticle.Text == "+", "build mode retains the centered placement reticle");
            Require(GameplayCameraCaptureAllowed, "build mode keeps camera look and cursor capture enabled");

            var initialPlayerPosition = _playerCharacter.GlobalPosition;
            var initialPreviewPosition = _hutPreview.GlobalPosition;
            SetProcess(true); _playerCharacter.SetPhysicsProcess(true);
            try
            {
                Input.ActionPress(ControllerActions.MoveForward);
                // The preview snaps to whole-metre placement cells; move far enough
                // that this short integration check crosses at least one cell.
                await PhysicsFrames(24);
            }
            finally
            {
                Input.ActionRelease(ControllerActions.MoveForward);
            }
            UpdateHutPreview();
            Require(_playerCharacter.GameplayInputEnabled
                && new Vector2(initialPlayerPosition.X, initialPlayerPosition.Z).DistanceTo(
                    new Vector2(_playerCharacter.GlobalPosition.X, _playerCharacter.GlobalPosition.Z)) > 0.1f,
                "player movement continues with the build hotbar open");
            Require(_hutPreview.GlobalPosition.DistanceTo(initialPreviewPosition) > 0.1f,
                $"selected preview follows player movement ({initialPreviewPosition} -> {_hutPreview.GlobalPosition})");
            var beforeLook = _hutPreview.GlobalPosition;
            _playerCharacter._UnhandledInput(new InputEventMouseMotion { Relative = new Vector2(35, 0) });
            UpdateHutPreview();
            Require(_hutPreview.GlobalPosition.DistanceTo(beforeLook) > 0.1f,
                "center camera ray moves the preview when the player looks around");

            SetProcess(false); _playerCharacter.SetPhysicsProcess(false);
            _playerCharacter.GlobalPosition = new Vector3(-6, 0, -1);
            _playerCharacter.Rotation = Vector3.Zero;
            playerCamera.Position = new Vector3(0, 2.6f, 5.5f);
            playerCamera.Rotation = new Vector3(_playerCharacter.CameraPitchOffset, 0, 0);
            SyncPlayerPosition(); UpdateHutPreview();
            Require(_placementValid, "camera-center ground ray returns to a valid starter hut footprint");
            _Input(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            var site = _world.ConstructionSites.Single();
            Require(!PlacementPanelOpen && _constructionViews.ContainsKey(new(TargetKind.ConstructionSite, site.Id)),
                "left click creates one authoritative targetable construction site");

            _playerCharacter.GlobalPosition = new Vector3(site.Position.X, 0, site.Position.Z + 1.5f); SyncPlayerPosition();
            _selectedTarget = new(TargetKind.ConstructionSite, site.Id);
            PerformFriendlyInteraction();
            Require(ConstructionPanelOpen, "F interaction opens close-range construction controls");
            for (var amount = 0; amount < SettlementConstruction.HutWood; amount++)
                PressMenuButton(_constructionPanel, "Supply 1 wood");
            for (var step = 0; step < SettlementConstruction.HutWork - 1; step++)
                PressMenuButton(_constructionPanel, "Apply work");
            Require(site.State == ConstructionSiteState.InProgress && site.Blueprint.WorkProgress == SettlementConstruction.HutWork - 1,
                "materials and work produce visible incomplete progress");
            PressMenuButton(_constructionPanel, "Apply work");
            var hut = _world.Structures.Single();
            Require(_world.ConstructionSites.Count == 0 && _selectedTarget == new WorldTarget(TargetKind.Structure, hut.Id),
                "completion replaces the site target with one hut");

            var hutView = _constructionViews[new(TargetKind.Structure, hut.Id)];
            var hutCollision = hutView.GetNode<StaticBody3D>("BuildingCollision");
            var hutCollisionParts = hutCollision.GetChildren().OfType<CollisionShape3D>()
                .Select(shape => shape.Name.ToString()).ToHashSet(StringComparer.Ordinal);
            Require(hutCollision.CollisionLayer == 2 && hutCollision.CollisionMask == 1
                && new[] { "Floor", "Ceiling", "WallLeft", "WallRight", "WallBack", "WallFrontLeft", "WallFrontRight" }
                    .All(hutCollisionParts.Contains),
                "completed hut has solid floor, ceiling and walls while preserving its doorway opening");
            var targets = WorldTargets();
            var hutPosition = new Vector3(hut.Position.X, hut.Position.Y, hut.Position.Z);
            Require(TargetLineClear(hutPosition + new Vector3(0, 1.2f, 3),
                hutPosition + new Vector3(0, 1.2f, 0), new(TargetKind.Structure, hut.Id), targets),
                "open doorway has a navigable clear center path");
            PerformFriendlyInteraction();
            Require(hut.IsClosed, "F toggles the completed hut door closed");
            hutView = _constructionViews[new(TargetKind.Structure, hut.Id)];
            Require(hutView.GetNode<StaticBody3D>("BuildingCollision").GetNodeOrNull<CollisionShape3D>("Door") is not null,
                "closed hut door receives a solid collision shape");
            var closedDoorNavigation = CreateNavigationMesh(CurrentWorkbenchPositions(), _world.Structures);
            Require(!HasNavigationAt(closedDoorNavigation, hutPosition + new Vector3(0, 0, 1.9f)),
                "closed hut door closes its navigation opening");
            targets = WorldTargets();
            Require(!TargetLineClear(hutPosition + new Vector3(0, 1.2f, 3),
                hutPosition + new Vector3(0, 1.2f, 0), new(TargetKind.Entity, "door-ray"), targets),
                "closed door blocks the doorway ray");
            PerformFriendlyInteraction();
            Require(!hut.IsClosed, "F reopens the completed hut door");
            var openDoorNavigation = CreateNavigationMesh(CurrentWorkbenchPositions(), _world.Structures);
            Require(HasNavigationAt(openDoorNavigation, hutPosition + new Vector3(0, 0, 1.9f))
                && HasNavigationAt(openDoorNavigation, hutPosition),
                "open doorway connects the exterior and usable interior in navigation");

            _playerCharacter.GlobalPosition = new Vector3(42, 0, 42); SyncPlayerPosition();
            playerCamera.Current = true;
            AimPlayerCameraAt(_playerCharacter.GlobalPosition);
            BeginBuildMode();
            _Input(new InputEventKey { PhysicalKeycode = Key.Key3, Keycode = Key.Key3, Pressed = true });
            UpdateWorkbenchPreview();
            Require(_placementGroundHit && _placementValid, "workbench preview is valid before checking a missed ground ray");
            var workbenchCountBeforeMissedRay = _world.Workstations.Count;
            playerCamera.Rotation = new Vector3(0.4f, 0, 0);
            _Input(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            Require(_world.Workstations.Count == workbenchCountBeforeMissedRay && PlacementPanelOpen && !_placementGroundHit,
                "a missed ground ray cannot place a fixture even when the previous preview was valid");
            _Input(new InputEventKey { PhysicalKeycode = Key.Escape, Keycode = Key.Escape, Pressed = true });

            foreach (var view in _entityViews.Values) view.Visible = false;
            var camera = GetNode<Camera3D>("Camera3D"); camera.Current = true;
            camera.GlobalPosition = hutView.GlobalPosition + new Vector3(7, 5, 8);
            camera.LookAt(hutView.GlobalPosition + new Vector3(0, 1.5f, 0));
            await PhysicsFrames(5);
            if (DisplayServer.GetName() != "headless")
            {
                var directory = ProjectSettings.GlobalizePath("res://../docs/evidence/construction");
                Directory.CreateDirectory(directory);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, "completed-hut.png")) == Error.Ok,
                    "actual rendered completed hut evidence");
            }

            SetProcess(false); _playerCharacter.SetPhysicsProcess(false);
            _playerCharacter.GlobalPosition = new Vector3(10, 0, 12); SyncPlayerPosition();
            playerCamera.Current = true;
            AimPlayerCameraAt(new Vector3(10, 0, 10));
            BeginBuildMode();
            _Input(new InputEventKey { PhysicalKeycode = Key.Key3, Keycode = Key.Key3, Pressed = true });
            var overlappingWorkbenchPosition = new Vector3(10, 0, 10);
            Require(!CanPlaceWorkbench(overlappingWorkbenchPosition, out var overlapReason)
                && overlapReason.Contains("workbench clearance", StringComparison.OrdinalIgnoreCase),
                "workbench validation rejects overlap with the original fixture");
            _Input(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            Require(_world.Workstations.Count == 1, "invalid workbench placement does not mutate the World");
            _Input(new InputEventKey { PhysicalKeycode = Key.Escape, Keycode = Key.Escape, Pressed = true });
            _playerCharacter.GlobalPosition = new Vector3(42, 0, 42); SyncPlayerPosition();
            AimPlayerCameraAt(_playerCharacter.GlobalPosition);
            var starterWorkbenchPosition = _world.Workstations.Single(item => item.Id == "workbench-1").Position;
            BeginBuildMode();
            _Input(new InputEventKey { PhysicalKeycode = Key.Key3, Keycode = Key.Key3, Pressed = true });
            Require(_workbenchPreview.Visible && _buildHotbarSlots[2].Label.Text.Contains("Workbench", StringComparison.Ordinal),
                "hotbar slot 3 previews an additional workbench while the original fixture remains in place");
            _Input(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            var addedWorkbench = _world.Workstations.Single(item => item.Id == "workbench-2");
            Require(addedWorkbench.Position == new System.Numerics.Vector3(42, 0, 42)
                && _world.Workstations.Single(item => item.Id == "workbench-1").Position == starterWorkbenchPosition
                && _workbenchViews.Count == 2,
                "placing slot 3 adds a separately presented World workstation without moving the starter workbench");
            var navigation = CreateNavigationMesh(CurrentWorkbenchPositions(), _world.Structures);
            Require(!HasNavigationAt(navigation, new Vector3(42, 0, 42)) && HasNavigationAt(navigation, new Vector3(45, 0, 42)),
                "generated navigation excludes the added workbench clearance while keeping the adjacent route open");
            var subdividedBuildingNavigation = CreateNavigationMesh([new System.Numerics.Vector3(18, 0, 42)]);
            Require(!HasNavigationAt(subdividedBuildingNavigation, new Vector3(15, 0, 15))
                && HasNavigationAt(subdividedBuildingNavigation, new Vector3(13, 0, 15)),
                "workbench grid subdivisions do not reopen scenery building interiors to navigation");
            Require(WorldTargets().Any(view => view.Target == new WorldTarget(TargetKind.Workstation, addedWorkbench.Id)),
                "the added workbench has an independent target-selection view");

            BeginBuildMode();
            _Input(new InputEventKey { PhysicalKeycode = Key.Key3, Keycode = Key.Key3, Pressed = true });
            AimPlayerCameraAt(new Vector3(48, 0, 42));
            _Input(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            var secondAddedWorkbench = _world.Workstations.Single(item => item.Id == "workbench-3");
            Require(secondAddedWorkbench.Id != addedWorkbench.Id && _workbenchViews.Count == 3,
                "slot 3 can place multiple workbenches with distinct identities");
            Require(addedWorkbench.InputStorage.TryAdd(ItemIds.Wood, 1) == ResourceMutationResult.Success,
                "additional workbench keeps its own independent input storage");
            _playerCharacter.GlobalPosition = new Vector3(42, 0, 44); SyncPlayerPosition();
            _selectedTarget = new(TargetKind.Workstation, addedWorkbench.Id);
            PerformFriendlyInteraction();
            Require(WorkstationPanelOpen && _openWorkstationId == addedWorkbench.Id,
                "the additional workbench opens through the existing target interaction");
            PressMenuButton(_workstationPanel, "Start basic meal");
            Require(addedWorkbench.InputStorage.TryAdd(ItemIds.Plant, 1) == ResourceMutationResult.Success,
                "the added workbench stores a separate recipe input");
            PressMenuButton(_workstationPanel, "Apply 1 work");
            Require(addedWorkbench.CurrentRecipe?.WorkProgress == 1
                && _world.Workstations.Single(item => item.Id == "workbench-1").CurrentRecipe?.WorkProgress == 0,
                $"the added workbench recipe progresses independently of the starter workbench (added {addedWorkbench.CurrentRecipe?.WorkProgress}, starter {_world.Workstations.Single(item => item.Id == "workbench-1").CurrentRecipe?.WorkProgress})");
            CloseWorkstation();

            BeginBuildMode();
            _Input(new InputEventKey { PhysicalKeycode = Key.Key4, Keycode = Key.Key4, Pressed = true });
            Require(_farmPreview.Visible && _buildHotbarSlots[3].Label.Text.Contains("Farm", StringComparison.Ordinal),
                "hotbar slot 4 previews an additional farm");
            AimPlayerCameraAt(new Vector3(42, 0, 50));
            _Input(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            var addedFarm = _world.Farms.Single(item => item.Id == "farm-2");
            Require(addedFarm.Plots.Count == 1 && _farmViews.ContainsKey(addedFarm.Id) && _world.Farms.Single(item => item.Id == "farm-1").Position == new System.Numerics.Vector3(5, 0, 5),
                "placing slot 4 adds a targetable farm with one empty plot and retains the starter farm");
            _playerCharacter.GlobalPosition = new Vector3(42, 0, 48); SyncPlayerPosition();
            _selectedTarget = new(TargetKind.Farm, addedFarm.Id);
            PerformFriendlyInteraction();
            Require(FarmPanelOpen && _openFarmId == addedFarm.Id,
                "the additional farm opens through the existing target interaction");
            CloseFarm();
            Require(addedFarm.Plots[0].TryPlant(SettlementCrops.CreateFieldCrop(), 1)
                && addedFarm.Plots[0].AdvanceGrowth(2), "additional farm crop state advances independently");

            _playerCharacter.GlobalPosition = new Vector3(50, 0, 50); SyncPlayerPosition();
            BeginBuildMode();
            _Input(new InputEventKey { PhysicalKeycode = Key.Key4, Keycode = Key.Key4, Pressed = true });
            AimPlayerCameraAt(new Vector3(50, 0, 50));
            _Input(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            var secondAddedFarm = _world.Farms.Single(item => item.Id == "farm-3");
            Require(secondAddedFarm.Id != addedFarm.Id && _farmViews.Count == 3
                && !secondAddedFarm.Plots[0].IsPlanted,
                "slot 4 can place multiple farms with distinct empty plot state");

            _saveSlotPath = ProjectSettings.GlobalizePath("res://../.construction-validation-save.json");
            SaveScenario(); LoadScenario();
            Require(_world.Structures.Single().Id == hut.Id && !_world.Structures.Single().IsClosed
                && _constructionViews.Count == 1, "save/load restores one open hut without stale site geometry");
            Require(_world.Workstations.Count == 3 && _world.Workstations.Single(item => item.Id == addedWorkbench.Id).Position == addedWorkbench.Position
                && _world.Workstations.Single(item => item.Id == addedWorkbench.Id).InputStorage.Counts.GetValueOrDefault(ItemIds.Wood) == 1
                && _world.Workstations.Single(item => item.Id == addedWorkbench.Id).InputStorage.Counts.GetValueOrDefault(ItemIds.Plant) == 1
                && _world.Workstations.Single(item => item.Id == addedWorkbench.Id).CurrentRecipe?.WorkProgress == 1
                && _world.Workstations.Single(item => item.Id == "workbench-1").CurrentRecipe?.WorkProgress == 0
                && _workbenchViews.Count == 3,
                "save/load restores multiple workbench positions, independent storage and recipe state, and presentation");
            Require(_world.Farms.Count == 3 && _world.Farms.Single(item => item.Id == addedFarm.Id).Plots[0].Growth == 2
                && !_world.Farms.Single(item => item.Id == secondAddedFarm.Id).Plots[0].IsPlanted
                && _farmViews.Count == 3 && WorldTargets().Any(view => view.Target == new WorldTarget(TargetKind.Farm, addedFarm.Id)),
                "save/load restores independent farm plots, multiple farm views, and target selection");
            ResetScenario(); SetProcess(false); _playerCharacter.SetPhysicsProcess(false);
            Require(_world.Structures.Count == 0 && _world.ConstructionSites.Count == 0 && _constructionViews.Count == 0,
                "reset clears constructed state and presentation");
            GD.Print("CONSTRUCTION VALIDATION PASSED: hotbar placement, preview, navigation clearance, fixture interactions, construction, reset/load and rendered evidence.");
            GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError($"CONSTRUCTION VALIDATION FAILED: {exception}"); GetTree().Quit(1); }
    }
}
