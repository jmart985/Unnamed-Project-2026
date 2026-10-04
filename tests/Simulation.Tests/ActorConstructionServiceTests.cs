using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class ActorConstructionServiceTests
{
    private static SpeciesCatalog Catalog => SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));

    [Fact]
    public void PlayerPlacesSuppliesBuildsAndUsesDurableHut()
    {
        var session = NewGameSession.Create(Catalog, PlayerCharacterProfile.Create("Builder"));
        var position = new Vector3(-6, 0, -6);
        session.World.TrySetEntityPosition(session.PlayerEntityId, position);
        session.Player.TryAddCarried(ItemIds.Wood, SettlementConstruction.HutWood);
        var service = new ActorConstructionService(session.World, Catalog);

        Assert.True(service.PlaceHut(session.PlayerEntityId, "hut-site-1", position).Succeeded);
        for (var amount = 0; amount < SettlementConstruction.HutWood; amount++)
            Assert.True(service.SupplyWood(session.PlayerEntityId, "hut-site-1").Succeeded);
        ConstructionAction result = null!;
        for (var step = 0; step < SettlementConstruction.HutWork; step++)
            Assert.True((result = service.ApplyWork(session.PlayerEntityId, "hut-site-1")).Succeeded);

        Assert.Empty(session.World.ConstructionSites);
        var hut = Assert.Single(session.World.Structures);
        Assert.Equal(result.CreatedId, hut.Id);
        var json = WorldSnapshotSerializer.Serialize(session.World, 3);
        var restored = new World();
        WorldSnapshotSerializer.Load(restored, json, Catalog);
        Assert.Equal(hut.Position, Assert.Single(restored.Structures).Position);
    }

    [Fact]
    public void InvalidPlacementAndFailedActionsDoNotMutateWorldOrInventory()
    {
        var session = NewGameSession.Create(Catalog, PlayerCharacterProfile.Create("Builder"));
        var service = new ActorConstructionService(session.World, Catalog, _ => true);
        var before = session.Player.Inventory.Count;

        Assert.Equal(ConstructionActionResult.OutOfRange,
            service.PlaceHut(session.PlayerEntityId, "far", new Vector3(20, 0, 20)).Result);
        Assert.Equal(ConstructionActionResult.InvalidTerrain,
            service.PlaceHut(session.PlayerEntityId, "blocked", session.Player.Position).Result);
        Assert.Empty(session.World.ConstructionSites);
        Assert.Equal(before, session.Player.Inventory.Count);
    }

    [Fact]
    public void NpcAndToolCapableCreatureCanBuildWhileIncapableCreatureCannot()
    {
        var session = NewGameSession.Create(Catalog, PlayerCharacterProfile.Create("Builder"));
        var position = new Vector3(-6, 0, -6);
        var npc = session.World.Entities.First(entity => entity.SpeciesId == "human" && entity.Id != session.PlayerEntityId);
        var ape = session.World.Entities.Single(entity => entity.SpeciesId == "ape");
        var cow = session.World.Entities.Single(entity => entity.SpeciesId == "cow");
        foreach (var actor in new[] { npc, ape, cow }) session.World.TrySetEntityPosition(actor.Id, position);
        npc.TryAddCarried(ItemIds.Wood, SettlementConstruction.HutWood);
        var service = new ActorConstructionService(session.World, Catalog);

        Assert.True(service.PlaceHut(npc.Id, "shared-site", position).Succeeded);
        for (var amount = 0; amount < SettlementConstruction.HutWood; amount++)
            Assert.True(service.SupplyWood(npc.Id, "shared-site").Succeeded);
        for (var step = 0; step < SettlementConstruction.HutWork; step++)
            Assert.True(service.ApplyWork(ape.Id, "shared-site").Succeeded);
        Assert.Single(session.World.Structures);
        Assert.Equal(ConstructionActionResult.ActorIncapable,
            service.PlaceHut(cow.Id, "cow-site", new Vector3(-12, 0, -12)).Result);
    }
}
