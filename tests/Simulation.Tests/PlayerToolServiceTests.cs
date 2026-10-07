using Simulation;

namespace Simulation.Tests;

public sealed class PlayerToolServiceTests
{
    [Fact]
    public void CraftsAxeFromTwoWoodAndOneRock()
    {
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Tool Tester"));
        session.Player.TryAddCarried(ItemIds.Wood, 2);
        session.Player.TryAddCarried(ItemIds.Rock, 1);

        var result = new PlayerToolService(session.World).CraftAxe();

        Assert.Equal(ToolCraftResultCode.Crafted, result.Code);
        Assert.Equal(1, session.Player.Inventory[ItemIds.Axe]);
        Assert.Equal(0, session.Player.Inventory.GetValueOrDefault(ItemIds.Wood));
        Assert.Equal(0, session.Player.Inventory.GetValueOrDefault(ItemIds.Rock));

        var equipped = new PlayerToolService(session.World).EquipAxe();
        Assert.Equal(ToolCraftResultCode.Crafted, equipped.Code);
        Assert.Equal(ItemIds.Axe, session.Player.EquippedTool);
    }

    [Fact]
    public void ChoppingRequiresEquippedAxeAndYieldsOneWood()
    {
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Chopper"), survivalGathering: true);
        var tree = session.World.ResourcePiles.Single(pile => pile.Id == "tree-adult-1");
        session.World.TrySetEntityPosition(session.PlayerEntityId, tree.Position);
        var service = new PlayerResourceService(session.World, catalog);

        Assert.Equal(PlayerResourceResultCode.NotUsable, service.ChopTree(tree.Id).Code);
        session.Player.TryAddCarried(ItemIds.Axe, 1);
        new PlayerToolService(session.World).EquipAxe();
        Assert.Equal(PlayerResourceResultCode.Collected, service.ChopTree(tree.Id).Code);
        Assert.Equal(1, session.Player.Inventory[ItemIds.Wood]);
        Assert.Equal(11, tree.Contents.Counts[ItemIds.Wood]);
    }

    [Fact]
    public void DepletedTreeRegrowsOnlyAfterTheConfiguredTimer()
    {
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Forester"), survivalGathering: true);
        var tree = session.World.ResourcePiles.Single(pile => pile.Id == "tree-adult-1");
        tree.Contents.TryRemove(ItemIds.Wood, tree.Contents.Counts[ItemIds.Wood]);
        var regrowth = new TreeRegrowthService(session.World);

        regrowth.Advance(TreeRegrowthService.RegrowthSeconds - 1);
        Assert.Equal(0, tree.Contents.Counts.GetValueOrDefault(ItemIds.Wood));
        Assert.InRange(regrowth.Progress(tree.Id), 0.9, 1);
        regrowth.Advance(1);
        Assert.Equal(TreeRegrowthService.AdultWoodCapacity, tree.Contents.Counts[ItemIds.Wood]);
        Assert.Equal(1, regrowth.Progress(tree.Id));
    }

    [Fact]
    public void SaveLoadPreservesEquippedAxe()
    {
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Saver"));
        session.Player.TryAddCarried(ItemIds.Axe, 1);
        new PlayerToolService(session.World).EquipAxe();
        var json = WorldSnapshotSerializer.Serialize(session.World, 12);
        var loaded = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Temp")).World;
        WorldSnapshotSerializer.Load(loaded, json, catalog);

        Assert.Equal(ItemIds.Axe, loaded.Entities.Single(entity => entity.Id == session.PlayerEntityId).EquippedTool);
    }

    [Fact]
    public void TreeRegrowthTimersRoundTripThroughServiceState()
    {
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Timer"), survivalGathering: true);
        var tree = session.World.ResourcePiles.Single(pile => pile.Id == "tree-adult-1");
        tree.Contents.TryRemove(ItemIds.Wood, tree.Contents.Counts[ItemIds.Wood]);
        var source = new TreeRegrowthService(session.World);
        source.Advance(30);
        var restored = new TreeRegrowthService(session.World);
        restored.Restore(source.Capture());
        Assert.Equal(source.Progress(tree.Id), restored.Progress(tree.Id));
    }

    [Fact]
    public void GatheredWoodCanBeDepositedIntoSettlementStorage()
    {
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Hauler"), survivalGathering: true);
        var storage = session.World.Stockpiles.Single();
        session.World.TrySetEntityPosition(session.PlayerEntityId, storage.Position);
        session.Player.TryAddCarried(ItemIds.Wood, 3);

        var result = new PlayerResourceService(session.World, catalog).StoreWood(storage.Id);

        Assert.Equal(PlayerResourceResultCode.Used, result.Code);
        Assert.Equal(3, storage.Contents.Counts[ItemIds.Wood]);
        Assert.Equal(0, session.Player.Inventory.GetValueOrDefault(ItemIds.Wood));
    }
}
