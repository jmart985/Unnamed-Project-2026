using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class CreatureSpawnerTests
{
    [Fact]
    public void SpawnsKnownSpeciesAndRejectsOccupiedHabitat()
    {
        var world = new World();
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var spawner = new CreatureSpawner(world, catalog);

        var spawned = spawner.Spawn(new("creature", "grove", Vector3.Zero, 7));
        var ape = spawner.Spawn(new("ape", "canopy", Vector3.One, 8));
        var occupied = spawner.Spawn(new("creature", "grove", Vector3.One, 8));

        Assert.Equal(CreatureSpawnStatus.Spawned, spawned.Status);
        Assert.Equal(CreatureSpawnStatus.Spawned, ape.Status);
        Assert.Equal(95, ape.Entity!.BaseStats.MaxHealth);
        Assert.Equal(2.4, ape.Entity.BaseStats.MovementSpeed);
        Assert.Equal(CreatureSpawnStatus.OccupiedHabitat, occupied.Status);
        Assert.Equal(2, world.Entities.Count);
    }

    [Fact]
    public void InvalidSpeciesReturnsTypedResultAndDespawnLeavesOtherEntities()
    {
        var world = new World();
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var spawner = new CreatureSpawner(world, catalog);
        var invalid = spawner.Spawn(new("missing", "grove", Vector3.Zero, 1));
        var first = spawner.Spawn(new("creature", "grove", Vector3.Zero, 1));
        var second = spawner.Spawn(new("creature", "lake", Vector3.One, 2));

        Assert.Equal(CreatureSpawnStatus.InvalidSpecies, invalid.Status);
        Assert.Null(invalid.Entity);
        Assert.Equal("entity-000001", first.Entity!.Id);
        Assert.True(spawner.Despawn(first.Entity!.Id));
        Assert.False(world.TryGetEntity(first.Entity.Id, out _));
        Assert.True(world.TryGetEntity(second.Entity!.Id, out _));
    }
}
