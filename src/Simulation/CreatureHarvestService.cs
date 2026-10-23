namespace Simulation;

public enum CreatureHarvestStatus { Harvested, InvalidEntity, InvalidAmount, NotDefeated }

public sealed record CreatureHarvestResult(CreatureHarvestStatus Status, FoodSource? Carcass = null, int MeatUnits = 0);

/// <summary>Turns a defeated creature into a temporary meat food source.</summary>
public static class CreatureHarvestService
{
    public static CreatureHarvestResult Harvest(World world, EntityRecord creature, int meatUnits, double freshnessSeconds = 300)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(creature);
        if (meatUnits <= 0) return new(CreatureHarvestStatus.InvalidAmount);
        if (!double.IsFinite(freshnessSeconds) || freshnessSeconds <= 0)
            return new(CreatureHarvestStatus.InvalidAmount);
        if (!world.TryGetEntity(creature.Id, out var liveCreature) || !ReferenceEquals(creature, liveCreature))
            return new(CreatureHarvestStatus.InvalidEntity);
        if (!creature.Health.IsDead) return new(CreatureHarvestStatus.NotDefeated);
        if (!world.TryRemoveEntity(creature.Id)) return new(CreatureHarvestStatus.InvalidEntity);

        var contents = new ResourceContainer($"carcass-{creature.Id}", meatUnits);
        contents.TryAdd(ItemIds.Meat, meatUnits);
        var carcass = world.AddFoodSource($"carcass-{creature.Id}", creature.Position, contents, freshnessSeconds);
        return new(CreatureHarvestStatus.Harvested, carcass, meatUnits);
    }
}
