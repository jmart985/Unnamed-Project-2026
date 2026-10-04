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
            var wood = _world.ResourcePiles.Single().Contents;
            Require(ResourceTransferService.TryPickUp(wood, player, ItemIds.Wood, SettlementConstruction.HutWood)
                == ResourceMutationResult.Success, "construction wood transferred atomically");

            ToggleMenu(PlayerMenu.Settlement);
            PressMenuButton(_controlsPanel, "Place starter hut");
            Require(PlacementPanelOpen && _hutPreview.Visible && _placementValid,
                "settlement control opens a valid in-world placement preview");
            PressMenuButton(_placementPanel, "Confirm site");
            var site = _world.ConstructionSites.Single();
            Require(!PlacementPanelOpen && _constructionViews.ContainsKey(new(TargetKind.ConstructionSite, site.Id)),
                "confirm creates one authoritative targetable site");

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
            var targets = WorldTargets();
            var hutPosition = new Vector3(hut.Position.X, hut.Position.Y, hut.Position.Z);
            Require(TargetLineClear(hutPosition + new Vector3(0, 1.2f, 3),
                hutPosition + new Vector3(0, 1.2f, 0), new(TargetKind.Structure, hut.Id), targets),
                "open doorway has a navigable clear center path");
            PerformFriendlyInteraction();
            Require(hut.IsClosed, "F toggles the completed hut door closed");
            targets = WorldTargets();
            Require(!TargetLineClear(hutPosition + new Vector3(0, 1.2f, 3),
                hutPosition + new Vector3(0, 1.2f, 0), new(TargetKind.Entity, "door-ray"), targets),
                "closed door blocks the doorway ray");
            PerformFriendlyInteraction();
            Require(!hut.IsClosed, "F reopens the completed hut door");

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

            _saveSlotPath = ProjectSettings.GlobalizePath("res://../.construction-validation-save.json");
            SaveScenario(); LoadScenario();
            Require(_world.Structures.Single().Id == hut.Id && !_world.Structures.Single().IsClosed
                && _constructionViews.Count == 1, "save/load restores one open hut without stale site geometry");
            ResetScenario(); SetProcess(false); _playerCharacter.SetPhysicsProcess(false);
            Require(_world.Structures.Count == 0 && _world.ConstructionSites.Count == 0 && _constructionViews.Count == 0,
                "reset clears constructed state and presentation");
            GD.Print("CONSTRUCTION VALIDATION PASSED: preview, atomic supply/work, completion, doorway, door, reset/load and rendered evidence.");
            GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError($"CONSTRUCTION VALIDATION FAILED: {exception}"); GetTree().Quit(1); }
    }
}
