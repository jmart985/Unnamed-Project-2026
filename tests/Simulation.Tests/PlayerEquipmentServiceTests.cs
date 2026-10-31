using Simulation;
using System.Text.Json.Nodes;

namespace Simulation.Tests;

public sealed class PlayerEquipmentServiceTests
{
    private static SpeciesCatalog Catalog() =>
        SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));

    [Theory]
    [InlineData(PlayerSex.Female)]
    [InlineData(PlayerSex.Male)]
    public void StarterWorkwearIsEquippedAndCanBeSwappedWithInventory(PlayerSex sex)
    {
        var session = NewGameSession.Create(Catalog(), PlayerCharacterProfile.Create("Wearer", sex: sex));
        var player = session.Player;
        var service = new PlayerEquipmentService(session.World);

        Assert.Equal(ItemIds.WorkShirt, player.EquippedItems[EquipmentSlotIds.Torso]);
        Assert.Equal(ItemIds.WorkTrousers, player.EquippedItems[EquipmentSlotIds.Legs]);
        Assert.Equal(1, player.Inventory[ItemIds.DeluxeBunnySuitTop]);
        Assert.Equal(1, player.Inventory[ItemIds.DeluxeBunnySuitLegs]);
        Assert.Equal(
            ItemCatalog.GetWeightKg(ItemIds.WorkShirt) + ItemCatalog.GetWeightKg(ItemIds.WorkTrousers)
                + ItemCatalog.GetWeightKg(ItemIds.DeluxeBunnySuitTop) + ItemCatalog.GetWeightKg(ItemIds.DeluxeBunnySuitLegs),
            player.CarriedWeightKg);

        var unequipped = service.Unequip(EquipmentSlotIds.Torso);
        Assert.Equal(PlayerEquipmentResultCode.Unequipped, unequipped.Code);
        Assert.False(player.EquippedItems.ContainsKey(EquipmentSlotIds.Torso));
        Assert.Equal(1, player.Inventory[ItemIds.WorkShirt]);
        Assert.Equal(
            ItemCatalog.GetWeightKg(ItemIds.WorkShirt) + ItemCatalog.GetWeightKg(ItemIds.WorkTrousers)
                + ItemCatalog.GetWeightKg(ItemIds.DeluxeBunnySuitTop) + ItemCatalog.GetWeightKg(ItemIds.DeluxeBunnySuitLegs),
            player.CarriedWeightKg);

        var equipped = service.Equip(ItemIds.WorkShirt);
        Assert.Equal(PlayerEquipmentResultCode.Equipped, equipped.Code);
        Assert.Equal(ItemIds.WorkShirt, player.EquippedItems[EquipmentSlotIds.Torso]);
        Assert.False(player.Inventory.ContainsKey(ItemIds.WorkShirt));

        Assert.Equal(PlayerEquipmentResultCode.Equipped, service.Equip(ItemIds.DeluxeBunnySuitTop).Code);
        Assert.Equal(ItemIds.DeluxeBunnySuitTop, player.EquippedItems[EquipmentSlotIds.Torso]);
        Assert.Equal(1, player.Inventory[ItemIds.WorkShirt]);
        Assert.False(player.Inventory.ContainsKey(ItemIds.DeluxeBunnySuitTop));
    }

    [Fact]
    public void DuplicateAndInvalidEquipRequestsLeaveEquipmentAndInventoryUnchanged()
    {
        var session = NewGameSession.Create(Catalog(), PlayerCharacterProfile.Create("Wearer"));
        var player = session.Player;
        var service = new PlayerEquipmentService(session.World);
        Assert.Equal(ResourceMutationResult.Success, player.TryAddCarried(ItemIds.WorkShirt, 1));
        Assert.Equal(ResourceMutationResult.Success, player.TryAddCarried(ItemIds.WorkTrousers, 1));

        Assert.Equal(PlayerEquipmentResultCode.AlreadyEquipped, service.Equip(ItemIds.WorkShirt).Code);
        Assert.Equal(PlayerEquipmentResultCode.AlreadyEquipped, service.Equip(ItemIds.WorkShirt).Code);
        Assert.Equal(PlayerEquipmentResultCode.AlreadyEquipped, service.Equip(ItemIds.WorkTrousers).Code);
        Assert.Equal(PlayerEquipmentResultCode.NotWearable, service.Equip(ItemIds.Food).Code);
        Assert.Equal(ItemIds.WorkShirt, player.EquippedItems[EquipmentSlotIds.Torso]);
        Assert.Equal(ItemIds.WorkTrousers, player.EquippedItems[EquipmentSlotIds.Legs]);
        Assert.Equal(1, player.Inventory[ItemIds.WorkShirt]);
        Assert.Equal(1, player.Inventory[ItemIds.WorkTrousers]);
        Assert.Equal(1, player.Inventory[ItemIds.DeluxeBunnySuitTop]);
        Assert.Equal(1, player.Inventory[ItemIds.DeluxeBunnySuitLegs]);
    }

    [Fact]
    public void SaveLoadPreservesEquippedClothingAndLegacySaveGetsStarterOutfit()
    {
        var catalog = Catalog();
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Saver", sex: PlayerSex.Male));
        var equipment = new PlayerEquipmentService(session.World);
        Assert.Equal(PlayerEquipmentResultCode.Equipped, equipment.Equip(ItemIds.DeluxeBunnySuitTop).Code);
        Assert.Equal(PlayerEquipmentResultCode.Equipped, equipment.Equip(ItemIds.DeluxeBunnySuitLegs).Code);
        var json = WorldSnapshotSerializer.Serialize(session.World, 12);
        var loaded = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Temp")).World;
        WorldSnapshotSerializer.Load(loaded, json, catalog);

        var loadedPlayer = loaded.Entities.Single(entity => entity.Id == session.PlayerEntityId);
        Assert.Equal(ItemIds.DeluxeBunnySuitTop, loadedPlayer.EquippedItems[EquipmentSlotIds.Torso]);
        Assert.Equal(ItemIds.DeluxeBunnySuitLegs, loadedPlayer.EquippedItems[EquipmentSlotIds.Legs]);
        Assert.Equal(1, loadedPlayer.Inventory[ItemIds.WorkShirt]);
        Assert.Equal(1, loadedPlayer.Inventory[ItemIds.WorkTrousers]);

        var legacy = JsonNode.Parse(WorldSnapshotSerializer.Serialize(session.World, 13))!.AsObject();
        legacy["SchemaVersion"] = 23;
        foreach (var entity in legacy["Entities"]!.AsArray())
            entity!.AsObject().Remove("EquippedItems");
        WorldSnapshotSerializer.Load(loaded, legacy.ToJsonString(), catalog);
        loadedPlayer = loaded.Entities.Single(entity => entity.Id == session.PlayerEntityId);
        Assert.Equal(ItemIds.WorkTrousers, loadedPlayer.EquippedItems[EquipmentSlotIds.Legs]);
        Assert.Equal(ItemIds.WorkShirt, loadedPlayer.EquippedItems[EquipmentSlotIds.Torso]);
        Assert.All(loaded.Entities.Where(entity => entity.Id != session.PlayerEntityId),
            entity => Assert.Empty(entity.EquippedItems));
    }

    [Fact]
    public void EquipmentVisibilityIsIndependentPerSlotAndDoesNotChangeEquipmentOrInventory()
    {
        var session = NewGameSession.Create(Catalog(), PlayerCharacterProfile.Create("Wearer"));
        var player = session.Player;
        var service = new PlayerEquipmentService(session.World);
        var carriedBefore = new Dictionary<string, int>(player.Inventory, StringComparer.Ordinal);

        Assert.Equal(PlayerEquipmentResultCode.VisibilityChanged,
            service.SetVisibility(EquipmentSlotIds.Torso, visible: false).Code);

        Assert.False(player.IsEquipmentVisible(EquipmentSlotIds.Torso));
        Assert.True(player.IsEquipmentVisible(EquipmentSlotIds.Legs));
        Assert.Equal(ItemIds.WorkShirt, player.EquippedItems[EquipmentSlotIds.Torso]);
        Assert.Equal(ItemIds.WorkTrousers, player.EquippedItems[EquipmentSlotIds.Legs]);
        Assert.Equal(carriedBefore.OrderBy(pair => pair.Key), player.Inventory.OrderBy(pair => pair.Key));
        Assert.Equal(PlayerEquipmentResultCode.VisibilityUnchanged,
            service.SetVisibility(EquipmentSlotIds.Torso, visible: false).Code);

        Assert.Equal(PlayerEquipmentResultCode.VisibilityChanged,
            service.SetVisibility(EquipmentSlotIds.Torso, visible: true).Code);
        Assert.True(player.IsEquipmentVisible(EquipmentSlotIds.Torso));
        Assert.Equal(carriedBefore.OrderBy(pair => pair.Key), player.Inventory.OrderBy(pair => pair.Key));

        Assert.Equal(PlayerEquipmentResultCode.NotEquipped,
            service.SetVisibility("head", visible: false).Code);
    }

    [Fact]
    public void ToolVisibilityRequiresAnEquippedToolAndPersistsAcrossSaveLoad()
    {
        var catalog = Catalog();
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Tool wearer"));
        var equipment = new PlayerEquipmentService(session.World);

        Assert.Equal(PlayerEquipmentResultCode.NotEquipped,
            equipment.SetVisibility(EquipmentSlotIds.Tool, visible: false).Code);

        Assert.Equal(ResourceMutationResult.Success, session.Player.TryAddCarried(ItemIds.Axe, 1));
        Assert.Equal(ToolCraftResultCode.Crafted, new PlayerToolService(session.World).EquipAxe().Code);
        Assert.Equal(PlayerEquipmentResultCode.VisibilityChanged,
            equipment.SetVisibility(EquipmentSlotIds.Tool, visible: false).Code);

        var json = WorldSnapshotSerializer.Serialize(session.World, 12);
        var loaded = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Temp")).World;
        WorldSnapshotSerializer.Load(loaded, json, catalog);
        var loadedPlayer = loaded.Entities.Single(entity => entity.Id == session.PlayerEntityId);

        Assert.Equal(ItemIds.Axe, loadedPlayer.EquippedTool);
        Assert.False(loadedPlayer.IsEquipmentVisible(EquipmentSlotIds.Tool));
        Assert.True(loadedPlayer.IsEquipmentVisible(EquipmentSlotIds.Torso));
        Assert.True(loadedPlayer.IsEquipmentVisible(EquipmentSlotIds.Legs));
    }

    [Fact]
    public void SaveLoadPreservesHiddenSlotsAndOldSnapshotsDefaultToVisible()
    {
        var catalog = Catalog();
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Wearer"));
        var equipment = new PlayerEquipmentService(session.World);
        Assert.Equal(PlayerEquipmentResultCode.VisibilityChanged,
            equipment.SetVisibility(EquipmentSlotIds.Legs, visible: false).Code);

        var json = WorldSnapshotSerializer.Serialize(session.World, 12);
        var loaded = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Temp")).World;
        WorldSnapshotSerializer.Load(loaded, json, catalog);
        var loadedPlayer = loaded.Entities.Single(entity => entity.Id == session.PlayerEntityId);
        Assert.False(loadedPlayer.IsEquipmentVisible(EquipmentSlotIds.Legs));
        Assert.True(loadedPlayer.IsEquipmentVisible(EquipmentSlotIds.Torso));
        Assert.Equal(ItemIds.WorkTrousers, loadedPlayer.EquippedItems[EquipmentSlotIds.Legs]);

        var legacy = JsonNode.Parse(json)!.AsObject();
        legacy["SchemaVersion"] = 24;
        foreach (var entity in legacy["Entities"]!.AsArray())
            entity!.AsObject().Remove("HiddenEquipmentSlots");
        WorldSnapshotSerializer.Load(loaded, legacy.ToJsonString(), catalog);
        loadedPlayer = loaded.Entities.Single(entity => entity.Id == session.PlayerEntityId);
        Assert.True(loadedPlayer.IsEquipmentVisible(EquipmentSlotIds.Torso));
        Assert.True(loadedPlayer.IsEquipmentVisible(EquipmentSlotIds.Legs));
    }

    [Fact]
    public void SnapshotRejectsHiddenSlotsWithoutEquipmentAndUnsupportedSlots()
    {
        var catalog = Catalog();
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Wearer"));
        var snapshot = JsonNode.Parse(WorldSnapshotSerializer.Serialize(session.World, 1))!.AsObject();
        var player = snapshot["Entities"]!.AsArray().Single(entity =>
            entity!["Id"]!.GetValue<string>() == session.PlayerEntityId)!.AsObject();
        player["HiddenEquipmentSlots"] = new JsonArray("head");

        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
            session.World, snapshot.ToJsonString(), catalog));

        player["HiddenEquipmentSlots"] = new JsonArray(EquipmentSlotIds.Tool);
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
            session.World, snapshot.ToJsonString(), catalog));
    }

    [Fact]
    public void SnapshotRejectsEquipmentInTheWrongSlotAndRepeatedItems()
    {
        var catalog = Catalog();
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Invalid"));
        var snapshot = JsonNode.Parse(WorldSnapshotSerializer.Serialize(session.World, 1))!.AsObject();
        var player = snapshot["Entities"]!.AsArray().Single(entity =>
            entity!["Id"]!.GetValue<string>() == session.PlayerEntityId)!.AsObject();
        player["EquippedItems"] = new JsonObject
        {
            [EquipmentSlotIds.Torso] = ItemIds.WorkShirt,
            [EquipmentSlotIds.Legs] = ItemIds.WorkShirt
        };

        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
            session.World, snapshot.ToJsonString(), catalog));
    }
}
