using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private async void RunBasicSettlementValidation()
    {
        try
        {
            StartSession(PlayerCharacterProfile.Create("Basic settlement validation"), 42, "Basic settlement acceptance validation.");
            SetProcess(false);
            _playerCharacter.SetPhysicsProcess(false);
            _saveSlotPath = ProjectSettings.GlobalizePath("res://../.basic-settlement-validation-save.json");
            var player = _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId);
            var stockpile = _world.Stockpiles.Single().Contents;

            RunWorkbenchAcceptanceStep(player);
            Require(player.Inventory.GetValueOrDefault(ItemIds.Food) == 2, "workbench controls produce and collect food through carried inventory");

            RunFarmAcceptanceStep(player);
            Require(player.Inventory.GetValueOrDefault(ItemIds.Food) == 5, "farm controls mature and harvest food into carried inventory");
            Require(ResourceTransferService.TryDeliver(player, stockpile, ItemIds.Food, 2) == ResourceMutationResult.Success,
                "stockpile deposit uses atomic inventory transfer before construction");

            var hut = RunConstructionAcceptanceStep(player);
            RunRestAcceptanceStep(player, hut);
            CaptureBasicSettlementEvidence(hut);
            await PhysicsFrames(5);
            if (DisplayServer.GetName() != "headless")
            {
                var directory = ProjectSettings.GlobalizePath("res://../docs/evidence/basic-settlement");
                Directory.CreateDirectory(directory);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, "playable-loop.png")) == Error.Ok,
                    "actual rendered integrated settlement evidence");
            }

            SaveScenario();
            var savedHutId = hut.Id;
            LoadScenario();
            player = _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId);
            var restoredHut = _world.Structures.Single(item => item.Id == savedHutId);
            Require(player.SleepState == SleepState.Sleeping && restoredHut.Beds.Single().OccupantId == player.Id,
                "save/load preserves completed hut bed occupancy and player rest state");
            PerformRestAction();
            Require(player.SleepState == SleepState.Awake && !restoredHut.Beds.Single().IsOccupied,
                "visible rest control wakes and releases the bed");

            ValidateMidProgressLoadAndRollback();
            ResetScenario(); SetProcess(false); _playerCharacter.SetPhysicsProcess(false);
            ResetScenario(); SetProcess(false); _playerCharacter.SetPhysicsProcess(false);
            Require(_world.Workstations.Count == 1 && _world.Farms.Count == 1 && _world.Structures.Count == 0
                && _world.ConstructionSites.Count == 0 && _constructionViews.Count == 0,
                "repeated reset restores one starter settlement without duplicate constructed state");

            GD.Print("BASIC SETTLEMENT VALIDATION PASSED: workbench, farm, construction, hut door, bed rest, save/load, invalid rollback, reset and rendered evidence.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"BASIC SETTLEMENT VALIDATION FAILED: {exception}");
            GetTree().Quit(1);
        }
    }

    private void RunWorkbenchAcceptanceStep(EntityRecord player)
    {
        var workbenchPosition = _world.Workstations.Single(workstation => workstation.Id == "workbench-1").Position;
        _playerCharacter.GlobalPosition = new Vector3(workbenchPosition.X, workbenchPosition.Y, workbenchPosition.Z - 1.5f);
        SyncPlayerPosition();
        Require(ResourceTransferService.TryPickUp(_world.FoodSources.First(source => source.Contents.Counts.ContainsKey(ItemIds.Plant)).Contents,
                player, ItemIds.Plant, 1) == ResourceMutationResult.Success
            && ResourceTransferService.TryPickUp(_world.ResourcePiles.Single().Contents,
                player, ItemIds.Wood, 1) == ResourceMutationResult.Success,
            "workbench ingredients transfer atomically from world resources");
        _selectedTarget = new(TargetKind.Workstation, "workbench-1");
        PerformFriendlyInteraction();
        Require(WorkstationPanelOpen, "workbench opens through visible target interaction");
        PressMenuButton(_workstationPanel, "Load 1 plant");
        PressMenuButton(_workstationPanel, "Load 1 wood");
        for (var step = 0; step < 5; step++) PressMenuButton(_workstationPanel, "Apply 1 work");
        PressMenuButton(_workstationPanel, "Collect output");
        CloseWorkstation();
    }

    private void RunFarmAcceptanceStep(EntityRecord player)
    {
        var farm = _world.Farms.Single();
        _playerCharacter.GlobalPosition = new Vector3(farm.Position.X, farm.Position.Y, farm.Position.Z - 1.7f);
        SyncPlayerPosition();
        Require(ResourceTransferService.TryPickUp(_world.FoodSources.First(source => source.Contents.Counts.ContainsKey(ItemIds.Plant)).Contents,
                player, ItemIds.Plant, 1) == ResourceMutationResult.Success,
            "farm seed transfers atomically from world resources");
        _selectedTarget = new(TargetKind.Farm, farm.Id);
        PerformFriendlyInteraction();
        Require(FarmPanelOpen, "farm opens through visible target interaction");
        PressMenuButton(_farmPanel, "Plant 1 plant");
        for (var step = 0; step < SettlementCrops.GrowthSteps; step++) PressMenuButton(_farmPanel, "Apply growth");
        PressMenuButton(_farmPanel, "Harvest food");
        CloseFarm();
    }

    private Structure RunConstructionAcceptanceStep(EntityRecord player)
    {
        Require(ResourceTransferService.TryPickUp(_world.ResourcePiles.Single().Contents,
                player, ItemIds.Wood, SettlementConstruction.HutWood) == ResourceMutationResult.Success,
            "construction wood transfers atomically from world resources");
        _playerCharacter.GlobalPosition = new Vector3(-6, 0, -1);
        _playerCharacter.Rotation = Vector3.Zero;
        SyncPlayerPosition();
        ToggleMenu(PlayerMenu.Settlement);
        PressMenuButton(_controlsPanel, "Place starter hut");
        Require(PlacementPanelOpen && _hutPreview.Visible && _placementValid,
            "settlement controls show a valid starter hut preview");
        PressMenuButton(_placementPanel, "Confirm site");
        var site = _world.ConstructionSites.Single();
        _playerCharacter.GlobalPosition = new Vector3(site.Position.X, 0, site.Position.Z + 1.5f);
        SyncPlayerPosition();
        _selectedTarget = new(TargetKind.ConstructionSite, site.Id);
        PerformFriendlyInteraction();
        Require(ConstructionPanelOpen, "construction site opens through visible target interaction");
        for (var amount = 0; amount < SettlementConstruction.HutWood; amount++) PressMenuButton(_constructionPanel, "Supply 1 wood");
        for (var step = 0; step < SettlementConstruction.HutWork; step++) PressMenuButton(_constructionPanel, "Apply work");
        var hut = _world.Structures.Single();
        Require(_world.ConstructionSites.Count == 0 && _selectedTarget == new WorldTarget(TargetKind.Structure, hut.Id),
            "construction completion replaces site with one targetable hut");
        PerformFriendlyInteraction();
        Require(hut.IsClosed, "completed hut door toggles closed through visible interaction");
        PerformFriendlyInteraction();
        Require(!hut.IsClosed, "completed hut door toggles open through visible interaction");
        return hut;
    }

    private void RunRestAcceptanceStep(EntityRecord player, Structure hut)
    {
        _playerCharacter.GlobalPosition = new Vector3(hut.Position.X, 0, hut.Position.Z + 1.2f);
        SyncPlayerPosition();
        _selectedTarget = new(TargetKind.Structure, hut.Id);
        UpdateWorldSelection();
        Require(_targetHud.Text.Contains("R rest/wake", StringComparison.Ordinal)
            && _targetHud.Text.Contains("bed available", StringComparison.Ordinal),
            "hut target HUD exposes available bed and rest control");
        PerformRestAction();
        Require(player.SleepState == SleepState.Sleeping && hut.Beds.Single().OccupantId == player.Id,
            "rest control reserves the shelter bed and enters sleeping state");
    }

    private void CaptureBasicSettlementEvidence(Structure hut)
    {
        foreach (var view in _entityViews.Values) view.Visible = false;
        var camera = GetNode<Camera3D>("Camera3D");
        camera.Current = true;
        camera.GlobalPosition = new Vector3(8, 7, 12);
        camera.LookAt(new Vector3(hut.Position.X * 0.45f, 0.8f, hut.Position.Z * 0.45f));
    }

    private void ValidateMidProgressLoadAndRollback()
    {
        ResetScenario(); SetProcess(false); _playerCharacter.SetPhysicsProcess(false);
        var player = _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId);
        _world.TrySetEntityPosition(player.Id, NewGameSession.TownWorkbenchPosition);
        Require(ResourceTransferService.TryPickUp(_world.FoodSources.First(source => source.Contents.Counts.ContainsKey(ItemIds.Plant)).Contents,
                player, ItemIds.Plant, 2) == ResourceMutationResult.Success
            && ResourceTransferService.TryPickUp(_world.ResourcePiles.Single().Contents, player, ItemIds.Wood, 4) == ResourceMutationResult.Success,
            "mid-progress materials transfer atomically");
        var workstation = new PlayerWorkstationService(_world);
        Require(workstation.LoadOne("workbench-1", ItemIds.Plant).Succeeded
            && workstation.LoadOne("workbench-1", ItemIds.Wood).Succeeded
            && workstation.ApplyWork("workbench-1").Succeeded,
            "workbench partial state established");
        var farm = _world.Farms.Single();
        _world.TrySetEntityPosition(player.Id, farm.Position);
        var farmService = new PlayerFarmService(_world, _speciesCatalog);
        Require(farmService.Plant(farm.Id, farm.Plots.Single().Id).Succeeded
            && farmService.AdvanceGrowth(farm.Id, farm.Plots.Single().Id).Succeeded,
            "farm partial state established");
        var builder = new ActorConstructionService(_world, _speciesCatalog, IsHutTerrainBlocked);
        _world.TrySetEntityPosition(player.Id, new System.Numerics.Vector3(-6, 0, -1));
        Require(builder.PlaceHut(player.Id, "mid-hut-site", new System.Numerics.Vector3(-6, 0, -6)).Succeeded,
            "construction site partial state established");
        var site = _world.ConstructionSites.Single();
        _world.TrySetEntityPosition(player.Id, site.Position);
        Require(builder.SupplyWood(player.Id, site.Id).Succeeded && builder.SupplyWood(player.Id, site.Id).Succeeded,
            "construction material progress established");
        var shelter = _world.AddStructure("validation-shelter", StructureType.Hut, new System.Numerics.Vector3(-10, 0, -6));
        shelter.AddBed(new Bed("validation-shelter-bed-1"));
        _world.TrySetEntityPosition(player.Id, shelter.Position);
        Require(new SettlementRestService(_world).Sleep(player.Id, shelter.Id).Succeeded, "rest state established before save");

        SaveScenario(); LoadScenario();
        player = _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId);
        Require(_world.Workstations.Single().CurrentRecipe!.WorkProgress == 1
            && _world.Farms.Single().Plots.Single().Growth == 1
            && _world.ConstructionSites.Single().Blueprint.GetProgress(ItemIds.Wood) == 2
            && _world.Structures.Single(item => item.Id == "validation-shelter").Beds.Single().OccupantId == player.Id
            && player.SleepState == SleepState.Sleeping,
            "mid-progress load resumes workstation, farm, construction site, structure, bed and rest state");

        var stableStructureCount = _world.Structures.Count;
        var stablePlayerState = player.SleepState;
        var invalid = WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime)
            .Replace("\"wood\": 2", "\"wood\": 99", StringComparison.Ordinal);
        File.WriteAllText(_saveSlotPath, invalid);
        LoadScenario();
        player = _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId);
        Require(_world.Structures.Count == stableStructureCount && player.SleepState == stablePlayerState
            && _saveLoadStatus.StartsWith("Load failed", StringComparison.Ordinal),
            "invalid construction save data rolls back without replacing live world");
    }
}
