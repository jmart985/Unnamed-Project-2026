using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class WorldTests
{
    [Fact]
    public void SpawnCreatesTenDistinctHumanRecords()
    {
        var world = new World();

        var entities = Enumerable.Range(0, 10)
            .Select(index => world.SpawnEntity("human", new Vector3(index, 0, 0)))
            .ToArray();

        Assert.Equal(10, entities.Length);
        Assert.Equal(10, entities.Select(entity => entity.Id).Distinct().Count());
        Assert.All(entities, entity =>
        {
            Assert.Equal("human", entity.SpeciesId);
            Assert.Equal(60, entity.Satiety);
            Assert.Empty(entity.Inventory);
            Assert.Equal("Idle", entity.Activity);
        });
        Assert.Equal(10, world.Entities.Count);
    }

    [Fact]
    public void LookupReturnsTheWorldOwnedRecordAndMissingIdsAreControlled()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);

        Assert.True(world.TryGetEntity(entity.Id, out var found));
        Assert.Same(entity, found);
        Assert.False(world.TryGetEntity("missing", out var missing));
        Assert.Null(missing);
        Assert.False(world.TryGetEntity(null, out var emptyId));
        Assert.Null(emptyId);
    }

    [Fact]
    public void RemovalAndRespawnDoNotReuseAnEntityId()
    {
        var world = new World();
        var removed = world.SpawnEntity("human", Vector3.Zero);

        Assert.True(world.TryRemoveEntity(removed.Id));
        Assert.False(world.TryRemoveEntity(removed.Id));
        Assert.False(world.TryGetEntity(removed.Id, out _));

        var replacement = world.SpawnEntity("human", Vector3.One);

        Assert.NotEqual(removed.Id, replacement.Id);
        Assert.Single(world.Entities);
    }

    [Fact]
    public void ResetClearsRecordsWithoutReusingIds()
    {
        var world = new World();
        var previous = world.SpawnEntity("human", Vector3.Zero);

        world.Reset();

        Assert.Empty(world.Entities);
        Assert.False(world.TryGetEntity(previous.Id, out _));

        var next = world.SpawnEntity("human", Vector3.One);

        Assert.NotEqual(previous.Id, next.Id);
        Assert.Equal("entity-000002", next.Id);
    }

    [Fact]
    public void InvalidSpawnInputsDoNotChangeWorldState()
    {
        var world = new World();

        Assert.Throws<ArgumentException>(() => world.SpawnEntity(" ", Vector3.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            world.SpawnEntity("human", new Vector3(float.NaN, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            world.SpawnEntity("human", Vector3.Zero, double.NaN));
        Assert.Empty(world.Entities);

        Assert.Equal("entity-000001", world.SpawnEntity("human", Vector3.Zero).Id);
    }
}
