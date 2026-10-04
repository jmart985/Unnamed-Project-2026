using System.Numerics;
using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class ConstructionSiteTests
{
    [Fact]
    public void ConstructionRequiresValidPlacementAndBuildsOnlyAfterMaterialsAndWorkAreSupplied()
    {
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Builder"), seed: 4);
        var player = session.Player;
        player.TryAddCarried(ItemIds.Wood, SettlementConstruction.HutWood);
        var service = new ActorConstructionService(session.World, catalog);

        Assert.False(service.PlaceHut(player.Id, "far-hut", new Vector3(100, 0, 100)).Succeeded);
        Assert.False(service.PlaceHut(player.Id, "invalid-hut", new Vector3(float.NaN, 0, 0)).Succeeded);
        Assert.True(service.PlaceHut(player.Id, "hut-site", new Vector3(10, 0, 3)).Succeeded);
        var site = Assert.Single(session.World.ConstructionSites);
        Assert.Equal(SettlementConstruction.HutWood, site.Blueprint.RequiredMaterials[ItemIds.Wood]);
        Assert.False(service.ApplyWork(player.Id, site.Id).Succeeded);
        Assert.Empty(session.World.Structures);

        session.World.TrySetEntityPosition(player.Id, site.Position);
        for (var index = 0; index < SettlementConstruction.HutWood; index++)
            Assert.True(service.SupplyWood(player.Id, site.Id).Succeeded);

        Assert.Empty(player.Inventory);
        Assert.False(service.SupplyWood(player.Id, site.Id).Succeeded);
        for (var index = 0; index < SettlementConstruction.HutWork; index++)
            Assert.True(service.ApplyWork(player.Id, site.Id).Succeeded);

        Assert.Empty(session.World.ConstructionSites);
        var hut = Assert.Single(session.World.Structures);
        Assert.Equal(site.Id + "-structure", hut.Id);
        Assert.Equal(StructureType.Hut, hut.Type);
        Assert.False(hut.IsClosed);
        Assert.Single(hut.Beds);
    }

    [Fact]
    public void RegistryRejectsDuplicatesAndReflectsBlueprintCompletion()
    {
        var blueprint = new ConstructionBlueprint("hut", 1, 1, new Dictionary<string,int> { [ItemIds.Wood] = 1 }, workRequired: 1);
        var site = new SettlementConstructionSite("site-1", Vector3.Zero, blueprint);
        var registry = new ConstructionSiteRegistry();
        Assert.True(registry.TryRegister(site)); Assert.False(registry.TryRegister(site));
        Assert.Equal(ConstructionSiteState.Planned, site.State);
        blueprint.ApplyProgress(ItemIds.Wood, 1);
        Assert.Equal(ConstructionSiteState.InProgress, site.State);
        Assert.True(blueprint.ApplyWork(1));
        Assert.Equal(ConstructionSiteState.Complete, site.State);
    }
}
