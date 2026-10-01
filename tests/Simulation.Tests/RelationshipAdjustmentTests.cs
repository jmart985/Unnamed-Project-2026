using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class RelationshipAdjustmentTests
{
    [Fact]
    public void AdjustmentCreatesAndUpdatesOnlyDirectedPair()
    {
        var world = new World();
        var observer = world.SpawnEntity("human", Vector3.Zero);
        var target = world.SpawnEntity("human", Vector3.One);

        var result = world.ApplyRelationshipAdjustment(
            observer.Id,
            target.Id,
            new RelationshipAdjustment(10, 20, 30, 40));

        Assert.Equal(RelationshipAdjustmentResult.Applied, result);
        Assert.True(world.TryGetRelationship(observer.Id, target.Id, out var relationship));
        Assert.Equal(10, relationship!.Familiarity);
        Assert.Equal(20, relationship.Trust);
        Assert.Equal(30, relationship.Fear);
        Assert.Equal(40, relationship.Loyalty);
        Assert.False(world.TryGetRelationship(target.Id, observer.Id, out _));
    }

    [Fact]
    public void AdjustmentClampsEveryDimensionToBounds()
    {
        var world = new World();
        var observer = world.SpawnEntity("human", Vector3.Zero);
        var target = world.SpawnEntity("human", Vector3.One);

        Assert.Equal(
            RelationshipAdjustmentResult.Applied,
            world.ApplyRelationshipAdjustment(
                observer.Id,
                target.Id,
                new RelationshipAdjustment(100, 100, 100, 100)));
        Assert.Equal(
            RelationshipAdjustmentResult.Applied,
            world.ApplyRelationshipAdjustment(
                observer.Id,
                target.Id,
                new RelationshipAdjustment(-100, -100, -100, -100)));
        Assert.True(world.TryGetRelationship(observer.Id, target.Id, out var relationship));
        Assert.Equal(0, relationship!.Familiarity);
        Assert.Equal(0, relationship.Trust);
        Assert.Equal(0, relationship.Fear);
        Assert.Equal(0, relationship.Loyalty);
    }

    [Theory]
    [InlineData(double.NaN, 0, 0, 0)]
    [InlineData(0, double.PositiveInfinity, 0, 0)]
    [InlineData(0, 0, double.NegativeInfinity, 0)]
    [InlineData(0, 0, 0, double.NaN)]
    public void AnyNonFiniteDeltaRejectsWithoutCreatingOrMutatingRelationship(
        double familiarity,
        double trust,
        double fear,
        double loyalty)
    {
        var world = new World();
        var observer = world.SpawnEntity("human", Vector3.Zero);
        var target = world.SpawnEntity("human", Vector3.One);

        Assert.Equal(
            RelationshipAdjustmentResult.InvalidDelta,
            world.ApplyRelationshipAdjustment(
                observer.Id,
                target.Id,
                new RelationshipAdjustment(familiarity, trust, fear, loyalty)));
        Assert.Empty(world.Relationships);

        Assert.Equal(
            RelationshipAdjustmentResult.Applied,
            world.ApplyRelationshipAdjustment(
                observer.Id,
                target.Id,
                new RelationshipAdjustment(0, 20, 0, 0)));
        Assert.True(world.TryGetRelationship(observer.Id, target.Id, out var existing));

        Assert.Equal(
            RelationshipAdjustmentResult.InvalidDelta,
            world.ApplyRelationshipAdjustment(
                observer.Id,
                target.Id,
                new RelationshipAdjustment(10, 0, double.PositiveInfinity, 0)));
        Assert.Same(existing, Assert.Single(world.Relationships));
        Assert.Equal(0, existing!.Familiarity);
        Assert.Equal(20, existing.Trust);
        Assert.Equal(0, existing.Fear);
        Assert.Equal(0, existing.Loyalty);
    }

    [Theory]
    [InlineData(null, "entity-000002")]
    [InlineData("missing", "entity-000002")]
    [InlineData("entity-000001", "missing")]
    public void MissingParticipantRejectsWithoutCreatingRelationship(
        string? observerId,
        string? targetId)
    {
        var world = new World();
        world.SpawnEntity("human", Vector3.Zero);
        world.SpawnEntity("human", Vector3.One);

        Assert.Equal(
            RelationshipAdjustmentResult.InvalidParticipant,
            world.ApplyRelationshipAdjustment(
                observerId,
                targetId,
                new RelationshipAdjustment(1, 2, 3, 4)));
        Assert.Empty(world.Relationships);
    }
}
