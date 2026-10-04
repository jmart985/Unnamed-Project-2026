using System.Numerics;
using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class ConstructionSiteTests
{
    [Fact]
    public void PlayerConstructionRequiresValidPlacementAndBuildsOnlyAfterMaterialsAreSupplied()
    {
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Builder"), seed: 4);
        var player = session.Player;
        player.TryAddCarried(ItemIds.Wood, 8);
        var service = new PlayerConstructionService(session.World, catalog);

        Assert.False(service.PlaceStarterHut(new System.Numerics.Vector3(100, 0, 100)).Succeeded);
        Assert.False(service.PlaceStarterHut(new System.Numerics.Vector3(float.NaN, 0, 0)).Succeeded);
        Assert.True(service.PlaceStarterHut(new System.Numerics.Vector3(10, 0, -3)).Succeeded);
        var site = Assert.Single(session.World.ConstructionSites);
        Assert.Equal(8, site.Blueprint.RequiredMaterials[ItemIds.Wood]);
        Assert.False(service.ApplyWork(site.Id).Succeeded);
        Assert.Empty(session.World.Structures);
        Assert.False(service.LoadWood(site.Id).Succeeded);
        session.World.TrySetEntityPosition(player.Id, site.Position);

        Assert.True(service.LoadWood(site.Id).Succeeded);
        Assert.Empty(player.Inventory);
        Assert.True(service.ApplyWork(site.Id).Succeeded);
        Assert.Empty(session.World.ConstructionSites);
        var hut = Assert.Single(session.World.Structures);
        Assert.Equal(site.Id, hut.Id);
        Assert.Equal(StructureType.Hut, hut.Type);
        Assert.False(hut.IsClosed);
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
