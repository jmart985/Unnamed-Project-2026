using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class PlayerAppearanceTests
{
    [Fact]
    public void JacketChangesDoNotReplaceTheShirtOrOtherSlots()
    {
        var jackets = PlayerAppearanceCatalog.Default.ForSlot(EquipmentSlotIds.Outerwear, PlayerSex.Female);
        Assert.True(jackets.Count >= 2);
        var appearance = PlayerAppearance.Bare with { Torso = ItemIds.WorkShirt, Outerwear = jackets[0].Id };
        var profile = PlayerCharacterProfile.Create("Layered clothing", sex: PlayerSex.Female, appearance: appearance);
        var session = NewGameSession.Create(Catalog(), profile);
        var player = session.Player;
        var service = new PlayerEquipmentService(session.World);
        Assert.Equal(ResourceMutationResult.Success, player.TryAddCarried(jackets[1].Id, 1));
        Assert.Equal(PlayerEquipmentResultCode.Equipped, service.Equip(jackets[1].Id).Code);
        Assert.Equal(ItemIds.WorkShirt, player.EquippedItems[EquipmentSlotIds.Torso]);
        Assert.Equal(jackets[1].Id, player.EquippedItems[EquipmentSlotIds.Outerwear]);
        Assert.Equal(1, player.Inventory[jackets[0].Id]);
        service.SetVisibility(EquipmentSlotIds.Outerwear, false);
        var restored = NewGameSession.Create(Catalog(), profile);
        WorldSnapshotSerializer.Load(restored.World, WorldSnapshotSerializer.Serialize(session.World, 2), Catalog());
        var loaded = restored.World.Entities.Single(entity => entity.Id == restored.World.PlayerEntityId);
        Assert.Equal(ItemIds.WorkShirt, loaded.EquippedItems[EquipmentSlotIds.Torso]);
        Assert.Equal(jackets[1].Id, loaded.EquippedItems[EquipmentSlotIds.Outerwear]);
        Assert.False(loaded.IsEquipmentVisible(EquipmentSlotIds.Outerwear));
        Assert.True(loaded.IsEquipmentVisible(EquipmentSlotIds.Torso));
    }
    private static SpeciesCatalog Catalog() =>
        SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));

    [Theory]
    [InlineData(PlayerSex.Male)]
    [InlineData(PlayerSex.Female)]
    public void BootsGlovesAndTorsoRemainIndependentThroughSwapsAndSaveLoad(PlayerSex sex)
    {
        var boots = PlayerAppearanceCatalog.Default.ForSlot(EquipmentSlotIds.Feet, sex).First();
        var gloves = PlayerAppearanceCatalog.Default.ForSlot(EquipmentSlotIds.Hands, sex).First();
        var appearance = PlayerAppearance.Bare with
        {
            Torso = ItemIds.WorkShirt, Feet = boots.Id, Hands = gloves.Id
        };
        var profile = PlayerCharacterProfile.Create("Independent slots", sex: sex, appearance: appearance);
        var session = NewGameSession.Create(Catalog(), profile);
        var service = new PlayerEquipmentService(session.World);
        Assert.Equal(PlayerEquipmentResultCode.Unequipped, service.Unequip(EquipmentSlotIds.Feet).Code);
        Assert.Equal(PlayerEquipmentResultCode.VisibilityChanged,
            service.SetVisibility(EquipmentSlotIds.Hands, false).Code);
        Assert.Equal(ItemIds.WorkShirt, session.Player.EquippedItems[EquipmentSlotIds.Torso]);
        Assert.Equal(gloves.Id, session.Player.EquippedItems[EquipmentSlotIds.Hands]);
        Assert.False(session.Player.EquippedItems.ContainsKey(EquipmentSlotIds.Feet));
        Assert.Equal(1, session.Player.Inventory[boots.Id]);
        var loaded = NewGameSession.Create(Catalog(), profile);
        WorldSnapshotSerializer.Load(loaded.World, WorldSnapshotSerializer.Serialize(session.World, 2), Catalog());
        var player = loaded.World.Entities.Single(entity => entity.Id == loaded.World.PlayerEntityId);
        Assert.Equal(ItemIds.WorkShirt, player.EquippedItems[EquipmentSlotIds.Torso]);
        Assert.Equal(gloves.Id, player.EquippedItems[EquipmentSlotIds.Hands]);
        Assert.False(player.IsEquipmentVisible(EquipmentSlotIds.Hands));
        Assert.Equal(PlayerEquipmentResultCode.Equipped,
            new PlayerEquipmentService(loaded.World).Equip(boots.Id).Code);
        Assert.Equal(gloves.Id, player.EquippedItems[EquipmentSlotIds.Hands]);
        Assert.False(player.IsEquipmentVisible(EquipmentSlotIds.Hands));
    }
    [Theory]
    [InlineData(PlayerSex.Male)]
    [InlineData(PlayerSex.Female)]
    public void BareCreationDefaultsOffAndOwnsNoEquippedClothing(PlayerSex sex)
    {
        var appearance = PlayerAppearance.Bare;
        Assert.False(appearance.ShowGenitalia);
        Assert.True(appearance.IsValid(sex));
        Assert.Empty(appearance.Equipment);
        var session = NewGameSession.Create(Catalog(),
            PlayerCharacterProfile.Create("Bare player", sex: sex, appearance: appearance));
        Assert.Empty(session.Player.EquippedItems);
    }

    [Theory]
    [InlineData(PlayerSex.Male)]
    [InlineData(PlayerSex.Female)]
    public void AppearanceAndSelectedEquipmentPersistWithoutDuplicatingWornItems(PlayerSex sex)
    {
        var torso = PlayerAppearanceCatalog.Default.ForSlot(EquipmentSlotIds.Torso, sex).First();
        var hair = PlayerAppearanceCatalog.Default.ForSlot("hair", sex).First();
        var appearance = PlayerAppearance.Bare with { ShowGenitalia = true, Hair = hair.Id, Torso = torso.Id };
        var session = NewGameSession.Create(Catalog(),
            PlayerCharacterProfile.Create("Appearance round trip", sex: sex, appearance: appearance));
        Assert.Equal(torso.Id, session.Player.EquippedItems[EquipmentSlotIds.Torso]);
        Assert.False(session.Player.Inventory.ContainsKey(torso.Id));
        var json = WorldSnapshotSerializer.Serialize(session.World, 3);
        var restored = WorldSnapshotSerializer.Deserialize(json);
        Assert.Equal(appearance, restored.PlayerProfile!.Appearance);
        Assert.Equal(torso.Id, restored.Entities.Single(entity => entity.Id == restored.PlayerEntityId)
            .EquippedItems![EquipmentSlotIds.Torso]);
    }

    [Fact]
    public void UnknownAndWrongSexAppearanceIdsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => PlayerCharacterProfile.Create("Unknown", appearance:
            PlayerAppearance.Bare with { Hair = "not_a_real_hair_asset" }));
        var femaleOnly = PlayerAppearanceCatalog.Default.Entries.First(entry => entry.Sexes.SequenceEqual([PlayerSex.Female]));
        Assert.False(PlayerAppearanceCatalog.Default.Supports(femaleOnly.Id, femaleOnly.Slot, PlayerSex.Male));
        Assert.False(PlayerAppearanceCatalog.Default.Supports(femaleOnly.Id, "not_a_real_slot", PlayerSex.Female));
    }

    [Fact]
    public void UnsupportedClothingCannotBeEquippedAndRejectionPreservesInventory()
    {
        var item = PlayerAppearanceCatalog.Default.Entries.First(entry => entry.Slot == EquipmentSlotIds.Torso
            && entry.Sexes.SequenceEqual([PlayerSex.Female]));
        var session = NewGameSession.Create(Catalog(), PlayerCharacterProfile.Create("Male", sex: PlayerSex.Male));
        Assert.Equal(ResourceMutationResult.Success, session.Player.TryAddCarried(item.Id, 1));
        var previous = session.Player.EquippedItems[EquipmentSlotIds.Torso];
        var result = new PlayerEquipmentService(session.World).Equip(item.Id);
        Assert.Equal(PlayerEquipmentResultCode.IncompatibleBody, result.Code);
        Assert.Equal(previous, session.Player.EquippedItems[EquipmentSlotIds.Torso]);
        Assert.Equal(1, session.Player.Inventory[item.Id]);
    }

    [Fact]
    public void Schema25LoadsWithAppearanceDefaultsAndExistingEquipment()
    {
        var session = NewGameSession.Create(Catalog(), PlayerCharacterProfile.Create("Legacy"));
        var node = JsonNode.Parse(WorldSnapshotSerializer.Serialize(session.World, 1))!.AsObject();
        node["SchemaVersion"] = 25;
        node["PlayerProfile"]!.AsObject().Remove("Appearance");
        var restored = WorldSnapshotSerializer.Deserialize(node.ToJsonString());
        Assert.Equal(WorldSnapshotSerializer.CurrentSchemaVersion, restored.SchemaVersion);
        Assert.Null(restored.PlayerProfile!.Appearance);
        Assert.False((restored.PlayerProfile.Appearance ?? new PlayerAppearance()).ShowGenitalia);
        Assert.Equal(ItemIds.WorkTrousers, restored.Entities.Single(entity => entity.Id == restored.PlayerEntityId)
            .EquippedItems![EquipmentSlotIds.Legs]);
    }
}
