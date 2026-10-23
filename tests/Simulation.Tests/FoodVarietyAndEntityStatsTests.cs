using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class FoodVarietyAndEntityStatsTests
{
    [Fact]
    public void CatalogLoadsDietaryProfilesAndBaseStats()
    {
        var catalog = Catalog();
        var human = catalog.GetRequired("human");
        var creature = catalog.GetRequired("creature");

        Assert.Equal(DietaryProfile.Omnivore, human.DietaryProfile);
        Assert.True(human.Eats(ItemIds.Meat));
        Assert.Equal(DietaryProfile.Herbivore, creature.DietaryProfile);
        Assert.False(creature.Eats(ItemIds.Meat));
        Assert.Equal(100, human.MaxHealth);
        Assert.Equal(1.2, creature.Endurance);
    }

    [Fact]
    public void CreatureHarvestCreatesMeatFoodSourceAndRemovesCreature()
    {
        var world = new World();
        var creature = world.SpawnEntity("creature", new Vector3(2, 0, 3));
        creature.Health.TryDamage("Torso", 50);

        var result = CreatureHarvestService.Harvest(world, creature, 4);

        Assert.Equal(CreatureHarvestStatus.Harvested, result.Status);
        Assert.Empty(world.Entities);
        Assert.Equal(4, result.Carcass!.Contents.Counts[ItemIds.Meat]);
        Assert.Equal(creature.Position, result.Carcass.Position);
    }

    [Fact]
    public void CreatureHarvestRejectsLiveEntitiesWithoutChangingWorld()
    {
        var world = new World();
        var creature = world.SpawnEntity("creature", new Vector3(2, 0, 3));

        var result = CreatureHarvestService.Harvest(world, creature, 4);

        Assert.Equal(CreatureHarvestStatus.NotDefeated, result.Status);
        Assert.Same(creature, Assert.Single(world.Entities));
        Assert.Empty(world.FoodSources);
    }

    private static SpeciesCatalog Catalog() => SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
}
