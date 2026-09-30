namespace Simulation;

public enum CreatureHarvestStatus { Harvested, InvalidEntity, InvalidAmount }

public sealed record CreatureHarvestResult(CreatureHarvestStatus Status, FoodSource? Carcass = null, int MeatUnits = 0);

/// <summary>Turns a defeated creature into a temporary meat food source.</summary>
public static class CreatureHarvestService
{
    public static CreatureHarvestResult Harvest(World world, EntityRecord creature, int meatUnits)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(creature);
        if (meatUnits <= 0) return new(CreatureHarvestStatus.InvalidAmount);
        if (!world.TryRemoveEntity(creature.Id)) return new(CreatureHarvestStatus.InvalidEntity);

        var contents = new ResourceContainer($"carcass-{creature.Id}", meatUnits);
        contents.TryAdd(ItemIds.Meat, meatUnits);
        var carcass = world.AddFoodSource($"carcass-{creature.Id}", creature.Position, contents);
        return new(CreatureHarvestStatus.Harvested, carcass, meatUnits);
    }
}
