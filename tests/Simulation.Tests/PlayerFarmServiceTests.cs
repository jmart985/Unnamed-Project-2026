using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class PlayerFarmServiceTests
{
    private static SpeciesCatalog Catalog => SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
    [Fact]
    public void PlayerCanPlantGrowAndHarvestACompleteCrop()
    {
        var session = CreateSession();
        session.World.TrySetEntityPosition(session.PlayerEntityId, new Vector3(5, 0, 5));
        session.Player.TryAddCarried(ItemIds.Plant, 1);
        var service = new PlayerFarmService(session.World, Catalog);

        Assert.True(service.Plant("farm-1", "farm-1-plot-1").Succeeded);
        for (var step = 0; step < SettlementCrops.GrowthSteps; step++)
            Assert.True(service.AdvanceGrowth("farm-1", "farm-1-plot-1").Succeeded);
        Assert.True(service.Harvest("farm-1", "farm-1-plot-1").Succeeded);

        Assert.Equal(SettlementCrops.HarvestFood, session.Player.Inventory[ItemIds.Food]);
        Assert.False(session.World.Farms.Single().Plots.Single().IsPlanted);
    }

    [Fact]
    public void RejectedPlantAndGrowthActionsLeaveStateUnchanged()
    {
        var session = CreateSession();
        session.World.TrySetEntityPosition(session.PlayerEntityId, new Vector3(5, 0, 5));
        var service = new PlayerFarmService(session.World, Catalog);

        Assert.Equal(FarmActionResult.MissingSeed, service.Plant("farm-1", "farm-1-plot-1").Result);
        Assert.Equal(FarmActionResult.PlotEmpty, service.AdvanceGrowth("farm-1", "farm-1-plot-1").Result);
        Assert.Equal(FarmActionResult.PlotEmpty, service.Harvest("farm-1", "farm-1-plot-1").Result);
        Assert.Equal(1, session.Player.Inventory[ItemIds.DeluxeBunnySuitTop]);
        Assert.Equal(1, session.Player.Inventory[ItemIds.DeluxeBunnySuitLegs]);
        Assert.Equal(2, session.Player.Inventory.Count);
        Assert.False(session.World.Farms.Single().Plots.Single().IsPlanted);

        session.World.TrySetEntityPosition(session.PlayerEntityId, new Vector3(7.01f, 0, 5));
        session.Player.TryAddCarried(ItemIds.Plant, 1);
        Assert.Equal(FarmActionResult.OutOfRange, service.Plant("farm-1", "farm-1-plot-1").Result);
        Assert.Equal(1, session.Player.Inventory[ItemIds.Plant]);
    }

    [Fact]
    public void PrematureAndFullItemStackHarvestsPreserveTheMatureCrop()
    {
        var session = CreateSession();
        session.World.TrySetEntityPosition(session.PlayerEntityId, new Vector3(5, 0, 5));
        session.Player.TryAddCarried(ItemIds.Plant, 1);
        var service = new PlayerFarmService(session.World, Catalog);
        service.Plant("farm-1", "farm-1-plot-1");

        Assert.Equal(FarmActionResult.CropImmature, service.Harvest("farm-1", "farm-1-plot-1").Result);
        var plot = session.World.Farms.Single().Plots.Single();
        Assert.Equal(0, plot.Growth);
        for (var step = 0; step < SettlementCrops.GrowthSteps; step++) service.AdvanceGrowth("farm-1", plot.Id);
        session.Player.TryAddCarried(ItemIds.Wood, 999);
        session.Player.TryAddCarried(ItemIds.Food, 999 - SettlementCrops.HarvestFood + 1);

        Assert.Equal(FarmActionResult.ItemStackLimitExceeded, service.Harvest("farm-1", plot.Id).Result);
        Assert.True(plot.IsMature);
        Assert.Equal(999 - SettlementCrops.HarvestFood + 1, session.Player.Inventory[ItemIds.Food]);
    }

    [Fact]
    public void IncapacitatedPlayerCannotChangeFarmState()
    {
        var session = CreateSession();
        session.World.TrySetEntityPosition(session.PlayerEntityId, new Vector3(5, 0, 5));
        session.Player.TryAddCarried(ItemIds.Plant, 1);
        var head = session.Player.Health.Parts.Single(part => part.Name == "Head");
        session.Player.Health.TryDamage("Head", head.MaximumHealth);

        var result = new PlayerFarmService(session.World, Catalog).Plant("farm-1", "farm-1-plot-1");

        Assert.Equal(FarmActionResult.ActorIncapacitated, result.Result);
        Assert.Equal(1, session.Player.Inventory[ItemIds.Plant]);
        Assert.False(session.World.Farms.Single().Plots.Single().IsPlanted);
    }

    [Fact]
    public void NpcAndToolCapableCreatureUseTheSameFarmActionsWhileIncapableCreatureIsRejected()
    {
        var session = CreateSession();
        var farm = session.World.Farms.Single();
        var npc = session.World.Entities.First(entity => entity.SpeciesId == "human" && entity.Id != session.PlayerEntityId);
        var ape = session.World.Entities.Single(entity => entity.SpeciesId == "ape");
        var cow = session.World.Entities.Single(entity => entity.SpeciesId == "cow");
        foreach (var actor in new[] { npc, ape, cow }) session.World.TrySetEntityPosition(actor.Id, farm.Position);
        npc.TryAddCarried(ItemIds.Plant, 1);
        var service = new ActorFarmService(session.World, Catalog);

        Assert.True(service.Plant(npc.Id, farm.Id, farm.Plots.Single().Id).Succeeded);
        for (var step = 0; step < SettlementCrops.GrowthSteps; step++)
            Assert.True(service.AdvanceGrowth(ape.Id, farm.Id, farm.Plots.Single().Id).Succeeded);
        Assert.True(service.Harvest(ape.Id, farm.Id, farm.Plots.Single().Id).Succeeded);
        Assert.Equal(SettlementCrops.HarvestFood, ape.Inventory[ItemIds.Food]);

        cow.TryAddCarried(ItemIds.Plant, 1);
        Assert.Equal(FarmActionResult.ActorIncapable, service.Plant(cow.Id, farm.Id, farm.Plots.Single().Id).Result);
        Assert.Equal(1, cow.Inventory[ItemIds.Plant]);
        Assert.False(farm.Plots.Single().IsPlanted);
    }

    private static NewGameSession CreateSession() => NewGameSession.Create(
        Catalog,
        PlayerCharacterProfile.Create("Farmer"));
}
