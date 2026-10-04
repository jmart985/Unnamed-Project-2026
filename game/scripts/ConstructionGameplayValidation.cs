using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private async void RunConstructionValidation()
    {
        try
        {
            StartSession(PlayerCharacterProfile.Create("Construction validation"), 4, "Starter hut construction validation.");
            SetProcess(false);
            _playerCharacter.SetPhysicsProcess(false);
            var player = _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId);
            Require(_world.ConstructionSites.Count == 0 && _world.Structures.Count == 0,
                "fresh session has no fabricated construction site or hut");

            var resourceService = new PlayerResourceService(_world, _speciesCatalog);
            var wood = _world.ResourcePiles.Single(item => item.Contents.Counts.ContainsKey(ItemIds.Wood));
            Require(resourceService.Collect(PlayerResourceKind.ResourcePile, wood.Id).Succeeded,
                "starter wood is collected through the existing bounded inventory flow");
            _world.TrySetEntityPosition(player.Id, new System.Numerics.Vector3(5, 0, 3));

            var buildButton = _menuHud.GetNode<Button>("BuildHut");
            buildButton.EmitSignal(Button.SignalName.Pressed);
            Require(ConstructionPanelOpen, "visible Build Hut control opens construction panel");
            PressMenuButton(_constructionPanel, "Place starter hut");
            Require(_hutPlacementActive && _hutPreview.Visible, "placement control enters visible preview mode");
            _hutPreview.GlobalPosition = new Vector3(10, 0, -3);
            HandleConstructionInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            Require(_world.ConstructionSites.Count == 1 && !_hutPlacementActive,
                "confirm input creates one authoritative site and exits preview");
            var site = _world.ConstructionSites.Single();
            Require(WorldTargets().Any(view => view.Target == new WorldTarget(TargetKind.ConstructionSite, site.Id)),
                "rendered site is targetable by its World identity");

            _world.TrySetEntityPosition(player.Id, site.Position);
            _selectedTarget = new(TargetKind.ConstructionSite, site.Id);
            PerformFriendlyInteraction();
            Require(ConstructionPanelOpen, "F interaction opens the site panel at close range");
            var woodCount = player.Inventory.GetValueOrDefault(ItemIds.Wood);
            PressMenuButton(_constructionPanel, "Load carried wood");
            Require(player.Inventory.GetValueOrDefault(ItemIds.Wood) == 0
                && site.Blueprint.GetProgress(ItemIds.Wood) == woodCount,
                "site loading consumes exactly the carried wood and exposes progress");

            _saveSlotPath = ProjectSettings.GlobalizePath("res://../.construction-validation-save.json");
            SaveScenario();
            LoadScenario();
            site = _world.ConstructionSites.Single();
            player = _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId);
            Require(site.Blueprint.GetProgress(ItemIds.Wood) == woodCount,
                "mid-progress save/load preserves blueprint material progress");
            _world.TrySetEntityPosition(player.Id, site.Position);
            _selectedTarget = new(TargetKind.ConstructionSite, site.Id);
            OpenConstructionSite(site.Id);
            PressMenuButton(_constructionPanel, "Apply build work");
            Require(_world.ConstructionSites.Count == 0 && _world.Structures.Count == 1,
                "work converts the site into one authoritative completed hut");

            var hut = _world.Structures.Single();
            Require(WorldTargets().Any(view => view.Target == new WorldTarget(TargetKind.Structure, hut.Id)),
                "completed hut remains selectable using its stable ID");
            _selectedTarget = new(TargetKind.Structure, hut.Id);
            var doorResult = _interactionResolver.Resolve(new InteractionRequest(player.Id, hut.Id,
                InteractionType.Physical, "toggle_door"));
            Require(doorResult.Succeeded && hut.IsClosed, "existing close-range door interaction toggles completed hut");

            foreach (var view in _entityViews.Values) view.Visible = false;
            var camera = GetNode<Camera3D>("Camera3D");
            camera.Current = true;
            camera.GlobalPosition = hut.Position + new Vector3(6, 5, 8);
            camera.LookAt(hut.Position + Vector3.Up);
            await PhysicsFrames(5);
            if (DisplayServer.GetName() != "headless")
            {
                var directory = ProjectSettings.GlobalizePath("res://../docs/evidence/construction");
                Directory.CreateDirectory(directory);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, "completed-hut.png")) == Error.Ok,
                    "actual rendered completed-hut evidence captured");
            }

            SaveScenario();
            LoadScenario();
            Require(_world.Structures.Count == 1 && _world.Structures.Single().IsClosed
                && _constructionViews.GetChildren().OfType<MeshInstance3D>().Count() == 1,
                "save/load restores door state and renders one hut without duplication");
            ResetScenario();
            ResetScenario();
            Require(_world.Structures.Count == 0 && _world.ConstructionSites.Count == 0
                && _constructionViews.GetChildCount() == 0,
                "repeated reset removes construction state and duplicate presentation");
            GD.Print("CONSTRUCTION VALIDATION PASSED: placement, target controls, material progress, snapshot restore, hut door, reset and rendered evidence.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"CONSTRUCTION VALIDATION FAILED: {exception}");
            GetTree().Quit(1);
        }
    }
}