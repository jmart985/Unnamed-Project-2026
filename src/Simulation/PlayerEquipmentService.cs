namespace Simulation;

public enum PlayerEquipmentResultCode
{
    Equipped,
    Unequipped,
    AlreadyEquipped,
    NotEquipped,
    NotWearable,
    MissingItem,
    InventoryFull,
    Unavailable,
    VisibilityChanged,
    VisibilityUnchanged
}

public sealed record PlayerEquipmentResult(PlayerEquipmentResultCode Code, string Message);

/// <summary>Swaps wearable items between the player's carried inventory and equipment slots.</summary>
public sealed class PlayerEquipmentService(World world)
{
    public PlayerEquipmentResult Equip(string? itemId)
    {
        if (!TryGetAvailablePlayer(out var player))
            return new(PlayerEquipmentResultCode.Unavailable, "The player cannot change equipment right now.");
        if (!ItemCatalog.IsWearable(itemId))
            return new(PlayerEquipmentResultCode.NotWearable, "That item cannot be equipped.");

        var item = ItemCatalog.GetRequired(itemId!);
        var slot = item.EquipmentSlot!;
        if (player.EquippedItems.GetValueOrDefault(slot) == item.Id)
            return new(PlayerEquipmentResultCode.AlreadyEquipped, "That item is already equipped.");
        if (player.Inventory.GetValueOrDefault(item.Id) < 1)
            return new(PlayerEquipmentResultCode.MissingItem, "Carry the item before equipping it.");

        var previous = player.EquippedItems.GetValueOrDefault(slot);
        if (previous is not null && !player.CanAddCarried(previous, 1))
            return new(PlayerEquipmentResultCode.InventoryFull, "There is no room to carry the item being replaced.");

        if (player.TryRemoveCarried(item.Id, 1) != ResourceMutationResult.Success)
            return new(PlayerEquipmentResultCode.MissingItem, "Carry the item before equipping it.");
        if (previous is not null && player.TryAddCarried(previous, 1) != ResourceMutationResult.Success)
        {
            player.TryAddCarried(item.Id, 1);
            return new(PlayerEquipmentResultCode.InventoryFull, "There is no room to carry the item being replaced.");
        }

        player.SetEquippedItem(slot, item.Id);
        return new(PlayerEquipmentResultCode.Equipped, $"Equipped {item.Id.Replace('_', ' ')}.");
    }

    public PlayerEquipmentResult Unequip(string? slotId)
    {
        if (!TryGetAvailablePlayer(out var player))
            return new(PlayerEquipmentResultCode.Unavailable, "The player cannot change equipment right now.");
        if (string.IsNullOrWhiteSpace(slotId)
            || !player.EquippedItems.TryGetValue(slotId, out var itemId))
            return new(PlayerEquipmentResultCode.NotEquipped, "Nothing is equipped in that slot.");
        if (!player.CanAddCarried(itemId, 1))
            return new(PlayerEquipmentResultCode.InventoryFull, "There is no room to carry that item.");
        if (player.TryAddCarried(itemId, 1) != ResourceMutationResult.Success)
            return new(PlayerEquipmentResultCode.InventoryFull, "There is no room to carry that item.");

        player.ClearEquippedItem(slotId);
        return new(PlayerEquipmentResultCode.Unequipped, $"Unequipped {itemId.Replace('_', ' ')}.");
    }

    public PlayerEquipmentResult SetVisibility(string? slotId, bool visible)
    {
        if (!TryGetAvailablePlayer(out var player))
            return new(PlayerEquipmentResultCode.Unavailable, "The player cannot change equipment right now.");
        if (string.IsNullOrWhiteSpace(slotId) || !player.HasVisibleEquipment(slotId))
            return new(PlayerEquipmentResultCode.NotEquipped, "There is no visible equipment in that slot.");
        return player.SetEquipmentVisible(slotId, visible)
            ? new(PlayerEquipmentResultCode.VisibilityChanged, visible ? "Equipment shown." : "Equipment hidden.")
            : new(PlayerEquipmentResultCode.VisibilityUnchanged, visible ? "Equipment is already shown." : "Equipment is already hidden.");
    }

    private bool TryGetAvailablePlayer(out EntityRecord player)
    {
        player = null!;
        return world.PlayerEntityId is { } id
            && world.TryGetEntity(id, out var found)
            && found is not null
            && !found.Health.IsIncapacitated
            && (player = found) is not null;
    }
}
