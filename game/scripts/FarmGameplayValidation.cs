using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private async void RunFarmValidation()
    {
        try
        {
            StartSession(PlayerCharacterProfile.Create("Farm validation"), 42, "Town farm validation.");
            SetProcess(false);
            _playerCharacter.SetPhysicsProcess(false);
            var farm = _world.Farms.Single();
            var plot = farm.Plots.Single();
            Require(farm.Id == "farm-1" && plot.Id == "farm-1-plot-1" && !plot.IsPlanted,
                "starter world contains one authoritative empty plot");
            Require(_farmView.IsVisibleInTree() && _farmView.FindChildren("*", "MeshInstance3D", true, false).Count >= 5
                && _farmCrop.GetChildCount() == 9,
                "visible bordered soil and crop components reuse the authoritative farm");

            _playerCharacter.GlobalPosition = new Vector3(farm.Position.X, farm.Position.Y, farm.Position.Z - 1.7f);
            SyncPlayerPosition();
            var player = _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId);
            var plantSource = _world.FoodSources.First(source => source.Contents.Counts.ContainsKey(ItemIds.Plant));
            Require(ResourceTransferService.TryPickUp(plantSource.Contents, player, ItemIds.Plant, 1) == ResourceMutationResult.Success,
                "seed resource transferred atomically into carried inventory");
            _selectedTarget = new(TargetKind.Farm, farm.Id);
            Require(WorldTargets().Any(view => view.Target == _selectedTarget), "rendered farm is a world target");
            PerformFriendlyInteraction();
            Require(FarmPanelOpen, "F interaction opens farm controls at close range");
            PressMenuButton(_farmPanel, "Plant 1 plant");
            Require(plot.IsPlanted && player.Inventory.GetValueOrDefault(ItemIds.Plant) == 0,
                "plant consumes exactly one carried resource");
            for (var step = 0; step < SettlementCrops.GrowthSteps; step++) PressMenuButton(_farmPanel, "Apply growth");
            Require(plot.IsMature && _farmLabel.Text.Contains("HARVEST READY", StringComparison.Ordinal),
                "growth controls and plot presentation reach mature state");

            foreach (var view in _entityViews.Values) view.Visible = false;
            var camera = GetNode<Camera3D>("Camera3D");
            camera.Current = true;
            camera.GlobalPosition = _farmView.GlobalPosition + new Vector3(5.2f, 4.2f, 6.2f);
            camera.LookAt(_farmView.GlobalPosition + new Vector3(0, 0.65f, 0));
            await PhysicsFrames(5);
            if (DisplayServer.GetName() != "headless")
            {
                var directory = ProjectSettings.GlobalizePath("res://../docs/evidence/farm");
                Directory.CreateDirectory(directory);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, "production-cycle.png")) == Error.Ok,
                    "actual rendered mature crop and farm controls evidence");
            }
            PressMenuButton(_farmPanel, "Harvest food");
            Require(!plot.IsPlanted && player.Inventory.GetValueOrDefault(ItemIds.Food) == SettlementCrops.HarvestFood,
                "harvest clears plot and adds exact bounded food yield");
            CloseFarm();

            ResetScenario(); SetProcess(false); _playerCharacter.SetPhysicsProcess(false);
            ResetScenario(); SetProcess(false); _playerCharacter.SetPhysicsProcess(false);
            Require(_world.Farms.Count == 1 && _farmView.FindChildren("*", "MeshInstance3D", true, false).Count >= 5
                && _farmCrop.GetChildCount() == 9,
                "repeated reset does not duplicate farms or rendered components");

            _saveSlotPath = ProjectSettings.GlobalizePath("res://../.farm-validation-save.json");
            farm = _world.Farms.Single(); plot = farm.Plots.Single();
            player = _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId);
            _world.TrySetEntityPosition(player.Id, farm.Position);
            Require(ResourceTransferService.TryPickUp(_world.FoodSources.First(source => source.Contents.Counts.ContainsKey(ItemIds.Plant)).Contents,
                    player, ItemIds.Plant, 1) == ResourceMutationResult.Success, "durability seed transferred atomically");
            var service = new PlayerFarmService(_world, _speciesCatalog);
            Require(service.Plant(farm.Id, plot.Id).Succeeded && service.AdvanceGrowth(farm.Id, plot.Id).Succeeded
                && service.AdvanceGrowth(farm.Id, plot.Id).Succeeded && service.AdvanceGrowth(farm.Id, plot.Id).Succeeded,
                "partial crop established before save");
            SaveScenario(); LoadScenario();
            var restored = _world.Farms.Single().Plots.Single();
            Require(restored.Crop?.Id == SettlementCrops.FieldCropId && restored.Growth == 3 && _farmView.IsVisibleInTree(),
                "save/load preserves crop identity, progress and rendered farm");
            GD.Print("FARM VALIDATION PASSED: selectable close-range controls, atomic plant/grow/harvest, reset, persistence and rendered evidence.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"FARM VALIDATION FAILED: {exception}");
            GetTree().Quit(1);
        }
    }
}
