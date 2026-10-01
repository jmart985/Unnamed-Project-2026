using System.Collections;
using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class WorldRelationshipTests
{
    [Fact]
    public void RelationshipCanBeCreatedAndRetrievedForExistingEntityPair()
    {
        var world = new World();
        var observer = world.SpawnEntity("human", Vector3.Zero);
        var target = world.SpawnEntity("human", Vector3.One);

        Assert.True(world.TryGetOrCreateRelationship(
            observer.Id,
            target.Id,
            out var created));
        Assert.NotNull(created);
        Assert.Equal(observer.Id, created.ObserverEntityId);
        Assert.Equal(target.Id, created.TargetEntityId);
        Assert.Single(world.Relationships);

        Assert.True(world.TryGetRelationship(
            observer.Id,
            target.Id,
            out var retrieved));
        Assert.Same(created, retrieved);
        Assert.True(world.TryGetOrCreateRelationship(
            observer.Id,
            target.Id,
            out var retrievedOrCreated));
        Assert.Same(created, retrievedOrCreated);
        Assert.Single(world.Relationships);
    }

    [Fact]
    public void ReverseDirectionHasIndependentRelationshipState()
    {
        var world = new World();
        var first = world.SpawnEntity("human", Vector3.Zero);
        var second = world.SpawnEntity("human", Vector3.One);

        Assert.True(world.TryGetOrCreateRelationship(first.Id, second.Id, out var forward));
        Assert.True(world.TryGetOrCreateRelationship(second.Id, first.Id, out var reverse));

        Assert.NotSame(forward, reverse);
        forward!.ApplyDelta(RelationshipDimension.Trust, 40);
        Assert.Equal(40, forward.Trust);
        Assert.Equal(0, reverse!.Trust);
        Assert.Collection(
            world.Relationships,
            relationship => Assert.Same(forward, relationship),
            relationship => Assert.Same(reverse, relationship));
    }

    [Theory]
    [InlineData(null, "entity-000001")]
    [InlineData("entity-000001", null)]
    [InlineData("", "entity-000001")]
    [InlineData("entity-000001", "")]
    [InlineData("missing", "entity-000001")]
    [InlineData("entity-000001", "missing")]
    public void MissingOrInvalidParticipantCannotCreateOrRetrieveRelationship(
        string? observerId,
        string? targetId)
    {
        var world = new World();
        world.SpawnEntity("human", Vector3.Zero);

        Assert.False(world.TryGetRelationship(observerId, targetId, out var existing));
        Assert.Null(existing);
        Assert.False(world.TryGetOrCreateRelationship(observerId, targetId, out var created));
        Assert.Null(created);
        Assert.Empty(world.Relationships);
    }

    [Fact]
    public void EntityCannotHaveRelationshipWithItself()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);

        Assert.False(world.TryGetRelationship(entity.Id, entity.Id, out var existing));
        Assert.Null(existing);
        Assert.False(world.TryGetOrCreateRelationship(entity.Id, entity.Id, out var created));
        Assert.Null(created);
        Assert.Equal(
            RelationshipAdjustmentResult.InvalidParticipant,
            world.ApplyRelationshipAdjustment(
                entity.Id,
                entity.Id,
                new RelationshipAdjustment(0, 1, 0, 0)));
        Assert.Empty(world.Relationships);
        Assert.Throws<ArgumentException>(() => new RelationshipState(entity.Id, entity.Id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RemovingEitherParticipantRemovesRelationshipInBothDirections(bool removeObserver)
    {
        var world = new World();
        var first = world.SpawnEntity("human", Vector3.Zero);
        var second = world.SpawnEntity("human", Vector3.One);

        Assert.True(world.TryGetOrCreateRelationship(first.Id, second.Id, out _));
        Assert.True(world.TryGetOrCreateRelationship(second.Id, first.Id, out _));

        Assert.True(world.TryRemoveEntity(removeObserver ? first.Id : second.Id));

        Assert.False(world.TryGetRelationship(first.Id, second.Id, out _));
        Assert.False(world.TryGetRelationship(second.Id, first.Id, out _));
        Assert.Empty(world.Relationships);
    }

    [Fact]
    public void ResetClearsRelationshipsAndExposedCollectionCannotBeMutated()
    {
        var world = new World();
        var first = world.SpawnEntity("human", Vector3.Zero);
        var second = world.SpawnEntity("human", Vector3.One);
        Assert.True(world.TryGetOrCreateRelationship(first.Id, second.Id, out _));

        var relationships = world.Relationships;
        Assert.Throws<NotSupportedException>(
            () => ((IList)relationships).Clear());

        world.Reset();

        Assert.Empty(world.Relationships);
        Assert.False(world.TryGetRelationship(first.Id, second.Id, out _));
    }
}
