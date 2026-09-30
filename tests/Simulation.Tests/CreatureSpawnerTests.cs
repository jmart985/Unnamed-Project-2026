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
        var occupied = spawner.Spawn(new("creature", "grove", Vector3.One, 8));

        Assert.Equal(CreatureSpawnStatus.Spawned, spawned.Status);
        Assert.Equal(CreatureSpawnStatus.OccupiedHabitat, occupied.Status);
        Assert.Single(world.Entities);
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
        Assert.True(spawner.Despawn(first.Entity!.Id));
        Assert.False(world.TryGetEntity(first.Entity.Id, out _));
        Assert.True(world.TryGetEntity(second.Entity!.Id, out _));
    }
}
