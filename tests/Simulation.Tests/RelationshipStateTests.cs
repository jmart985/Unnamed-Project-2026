using Simulation;

namespace Simulation.Tests;

public sealed class RelationshipStateTests
{
    [Fact]
    public void NewRelationshipStartsNeutralForAnOrderedEntityPair()
    {
        var relationship = new RelationshipState("entity-1", "entity-2");

        Assert.Equal("entity-1", relationship.ObserverEntityId);
        Assert.Equal("entity-2", relationship.TargetEntityId);
        Assert.Equal(0, relationship.Familiarity);
        Assert.Equal(0, relationship.Trust);
        Assert.Equal(0, relationship.Fear);
        Assert.Equal(0, relationship.Loyalty);
    }

    [Fact]
    public void RelationshipValuesAreDirectional()
    {
        var forward = new RelationshipState("entity-1", "entity-2");
        var reverse = new RelationshipState("entity-2", "entity-1");

        Assert.Equal(
            RelationshipUpdateResult.Applied,
            forward.ApplyDelta(RelationshipDimension.Trust, 35));

        Assert.Equal(35, forward.Trust);
        Assert.Equal(0, reverse.Trust);
    }

    [Theory]
    [InlineData(RelationshipDimension.Familiarity)]
    [InlineData(RelationshipDimension.Trust)]
    [InlineData(RelationshipDimension.Fear)]
    [InlineData(RelationshipDimension.Loyalty)]
    public void FiniteDeltasClampAtBothBounds(RelationshipDimension dimension)
    {
        var relationship = new RelationshipState("entity-1", "entity-2");

        Assert.Equal(
            RelationshipUpdateResult.Applied,
            relationship.ApplyDelta(dimension, double.MaxValue));
        Assert.Equal(100, GetValue(relationship, dimension));

        Assert.Equal(
            RelationshipUpdateResult.Applied,
            relationship.ApplyDelta(dimension, double.MinValue));
        Assert.Equal(0, GetValue(relationship, dimension));
    }

    [Theory]
    [InlineData(null, "entity-2")]
    [InlineData("", "entity-2")]
    [InlineData(" ", "entity-2")]
    [InlineData("entity-1", null)]
    [InlineData("entity-1", "")]
    [InlineData("entity-1", " ")]
    public void InvalidEntityIdsAreRejected(string? observerId, string? targetId)
    {
        Assert.Throws<ArgumentException>(() => new RelationshipState(observerId!, targetId!));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteDeltasAreRejectedWithoutMutation(double delta)
    {
        var relationship = new RelationshipState("entity-1", "entity-2");
        relationship.ApplyDelta(RelationshipDimension.Trust, 20);

        Assert.Equal(
            RelationshipUpdateResult.InvalidDelta,
            relationship.ApplyDelta(RelationshipDimension.Trust, delta));
        Assert.Equal(20, relationship.Trust);
    }

    [Fact]
    public void InvalidDimensionIsRejectedWithoutMutation()
    {
        var relationship = new RelationshipState("entity-1", "entity-2");

        Assert.Equal(
            RelationshipUpdateResult.InvalidDimension,
            relationship.ApplyDelta((RelationshipDimension)int.MaxValue, 10));
        Assert.Equal(0, relationship.Trust);
    }

    private static double GetValue(RelationshipState relationship, RelationshipDimension dimension)
    {
        return dimension switch
        {
            RelationshipDimension.Familiarity => relationship.Familiarity,
            RelationshipDimension.Trust => relationship.Trust,
            RelationshipDimension.Fear => relationship.Fear,
            RelationshipDimension.Loyalty => relationship.Loyalty,
            _ => throw new ArgumentOutOfRangeException(nameof(dimension))
        };
    }
}
