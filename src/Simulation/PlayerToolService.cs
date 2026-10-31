namespace Simulation;

public enum ToolCraftResultCode { Crafted, Unavailable, MissingMaterials, InventoryFull }
public sealed record ToolCraftResult(ToolCraftResultCode Code, string Message);

public sealed class PlayerToolService(World world)
{
    public ToolCraftResult EquipAxe()
    {
        if (world.PlayerEntityId is not { } id || !world.TryGetEntity(id, out var player) || player is null || player.Health.IsIncapacitated)
            return new(ToolCraftResultCode.Unavailable, "The player cannot equip tools while incapacitated.");
        if (player.Inventory.GetValueOrDefault(ItemIds.Axe) < 1)
            return new(ToolCraftResultCode.MissingMaterials, "Craft an axe before equipping it.");
        player.SetEquippedTool(ItemIds.Axe);
        return new(ToolCraftResultCode.Crafted, "Equipped the axe.");
    }

    public ToolCraftResult CraftAxe()
    {
        if (world.PlayerEntityId is not { } id || !world.TryGetEntity(id, out var player) || player is null || player.Health.IsIncapacitated)
            return new(ToolCraftResultCode.Unavailable, "The player cannot craft while incapacitated.");
        if (player.Inventory.GetValueOrDefault(ItemIds.Wood) < 2 || player.Inventory.GetValueOrDefault(ItemIds.Rock) < 1)
            return new(ToolCraftResultCode.MissingMaterials, "Crafting an axe requires 2 wood and 1 rock.");
        player.TryRemoveCarried(ItemIds.Wood, 2);
        player.TryRemoveCarried(ItemIds.Rock, 1);
        var result = player.TryAddCarried(ItemIds.Axe, 1);
        if (result != ResourceMutationResult.Success)
        {
            player.TryAddCarried(ItemIds.Wood, 2);
            player.TryAddCarried(ItemIds.Rock, 1);
            return new(ToolCraftResultCode.InventoryFull, "There is no room to carry the crafted axe.");
        }
        return new(ToolCraftResultCode.Crafted, "Crafted an axe from gathered wood and rock. The axe is ready to equip.");
    }
}
