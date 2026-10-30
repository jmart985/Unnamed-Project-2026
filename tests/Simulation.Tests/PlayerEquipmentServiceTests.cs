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
        Assert.Empty(player.Inventory);
        Assert.Equal(
            ItemCatalog.GetWeightKg(ItemIds.WorkShirt) + ItemCatalog.GetWeightKg(ItemIds.WorkTrousers),
            player.CarriedWeightKg);

        var unequipped = service.Unequip(EquipmentSlotIds.Torso);
        Assert.Equal(PlayerEquipmentResultCode.Unequipped, unequipped.Code);
        Assert.False(player.EquippedItems.ContainsKey(EquipmentSlotIds.Torso));
        Assert.Equal(1, player.Inventory[ItemIds.WorkShirt]);
        Assert.Equal(
            ItemCatalog.GetWeightKg(ItemIds.WorkShirt) + ItemCatalog.GetWeightKg(ItemIds.WorkTrousers),
            player.CarriedWeightKg);

        var equipped = service.Equip(ItemIds.WorkShirt);
        Assert.Equal(PlayerEquipmentResultCode.Equipped, equipped.Code);
        Assert.Equal(ItemIds.WorkShirt, player.EquippedItems[EquipmentSlotIds.Torso]);
        Assert.False(player.Inventory.ContainsKey(ItemIds.WorkShirt));
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
    }

    [Fact]
    public void SaveLoadPreservesEquippedClothingAndLegacySaveGetsStarterOutfit()
    {
        var catalog = Catalog();
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Saver", sex: PlayerSex.Male));
        new PlayerEquipmentService(session.World).Unequip(EquipmentSlotIds.Legs);
        var json = WorldSnapshotSerializer.Serialize(session.World, 12);
        var loaded = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Temp")).World;
        WorldSnapshotSerializer.Load(loaded, json, catalog);

        var loadedPlayer = loaded.Entities.Single(entity => entity.Id == session.PlayerEntityId);
        Assert.Equal(ItemIds.WorkShirt, loadedPlayer.EquippedItems[EquipmentSlotIds.Torso]);
        Assert.False(loadedPlayer.EquippedItems.ContainsKey(EquipmentSlotIds.Legs));
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
