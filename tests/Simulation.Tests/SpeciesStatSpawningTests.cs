using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class SpeciesStatSpawningTests
{
    [Theory]
    [InlineData("ape", 95, 2.4, 1.1, 1.0)]
    [InlineData("cow", 120, 1.8, 1.4, 1.3)]
    [InlineData("wolf", 85, 3.4, 1.0, 1.1)]
    public void SpeciesAwareWorldSpawningUsesSpeciesStatsAndConfiguredFallbacks(
        string speciesId,
        double maxHealth,
        double movementSpeed,
        double strength,
        double endurance)
    {
        var defaults = new CharacterStats(
            health: 150,
            stamina: 70,
            movementSpeed: 4,
            carryCapacity: 12,
            perception: 2,
            defense: 3,
            strength: 2,
            endurance: 2);
        var world = new World(defaultCharacterStats: defaults);
        var species = LoadCatalog().GetRequired(speciesId);

        var entity = world.SpawnEntity(species, Vector3.Zero);

        Assert.Equal(maxHealth, entity.BaseStats.MaxHealth);
        Assert.Equal(movementSpeed, entity.BaseStats.MovementSpeed);
        Assert.Equal(strength, entity.BaseStats.Strength);
        Assert.Equal(endurance, entity.BaseStats.Endurance);
        Assert.Equal(defaults.MaxStamina, entity.BaseStats.MaxStamina);
        Assert.Equal(defaults.CarryCapacity, entity.BaseStats.CarryCapacity);
        Assert.Equal(defaults.Perception, entity.BaseStats.Perception);
        Assert.Equal(defaults.Defense, entity.BaseStats.Defense);
        Assert.Equal(maxHealth / 2, entity.Health.Parts.Single(part => part.Name == "Torso").MaximumHealth);
    }

    [Fact]
    public void SpawnWithoutSpeciesDefinitionPreservesConfiguredStats()
    {
        var defaults = new CharacterStats(health: 125, movementSpeed: 4, carryCapacity: 20);
        var world = new World(defaultCharacterStats: defaults);

        var entity = world.SpawnEntity("ape", Vector3.Zero);

        Assert.Same(defaults, entity.BaseStats);
    }

    [Fact]
    public void InvalidSpeciesAwareSpawnDoesNotMutateWorldOrConsumeAnEntityId()
    {
        var world = new World();
        var species = LoadCatalog().GetRequired("wolf");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            world.SpawnEntity(species, new Vector3(float.NaN, 0, 0)));
        Assert.Empty(world.Entities);

        var spawned = world.SpawnEntity(species, Vector3.Zero);

        Assert.Equal("entity-000001", spawned.Id);
        Assert.Single(world.Entities);
    }

    [Fact]
    public void SnapshotRestoreRebuildsSpeciesStatsFromTheCatalog()
    {
        var catalog = LoadCatalog();
        var source = new World();
        source.SpawnEntity(catalog.GetRequired("cow"), Vector3.Zero);
        var json = WorldSnapshotSerializer.Serialize(source, simulationTime: 0);
        var target = new World();

        WorldSnapshotSerializer.Load(target, json, catalog);

        var restored = Assert.Single(target.Entities);
        Assert.Equal(120, restored.BaseStats.MaxHealth);
        Assert.Equal(1.8, restored.BaseStats.MovementSpeed);
        Assert.Equal(1.4, restored.BaseStats.Strength);
        Assert.Equal(1.3, restored.BaseStats.Endurance);
        Assert.Equal(60, restored.Satiety);
    }

    private static SpeciesCatalog LoadCatalog()
    {
        return SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
    }
}
