using Simulation;

namespace Simulation.Tests;

public sealed class RelationshipBondClassifierTests
{
    [Fact]
    public void DefaultThresholdsClassifyEachRelationshipStageAtItsBoundary()
    {
        var relationship = new RelationshipState("entity-1", "entity-2");
        Assert.Equal(
            RelationshipBondLevel.Stranger,
            RelationshipBondClassifier.Classify(relationship));

        relationship.ApplyDelta(RelationshipDimension.Familiarity, 20);
        Assert.Equal(
            RelationshipBondLevel.Familiar,
            RelationshipBondClassifier.Classify(relationship));

        relationship.ApplyDelta(RelationshipDimension.Trust, 50);
        Assert.Equal(
            RelationshipBondLevel.Trusted,
            RelationshipBondClassifier.Classify(relationship));

        relationship.ApplyDelta(RelationshipDimension.Trust, 25);
        relationship.ApplyDelta(RelationshipDimension.Loyalty, 60);
        relationship.ApplyDelta(RelationshipDimension.Fear, 20);
        Assert.Equal(
            RelationshipBondLevel.Loyal,
            RelationshipBondClassifier.Classify(relationship));
    }

    [Fact]
    public void FearfulClassificationTakesPrecedenceOverOtherThresholds()
    {
        var relationship = new RelationshipState("entity-1", "entity-2");
        relationship.ApplyDelta(RelationshipDimension.Familiarity, 100);
        relationship.ApplyDelta(RelationshipDimension.Trust, 100);
        relationship.ApplyDelta(RelationshipDimension.Loyalty, 100);
        relationship.ApplyDelta(RelationshipDimension.Fear, 50);

        Assert.Equal(
            RelationshipBondLevel.Fearful,
            RelationshipBondClassifier.Classify(relationship));
    }

    [Fact]
    public void LoyaltyRequiresBothTrustAndLoyaltyThresholdsAndLowFear()
    {
        var relationship = new RelationshipState("entity-1", "entity-2");
        relationship.ApplyDelta(RelationshipDimension.Familiarity, 20);
        relationship.ApplyDelta(RelationshipDimension.Trust, 75);
        relationship.ApplyDelta(RelationshipDimension.Fear, 21);

        Assert.Equal(
            RelationshipBondLevel.Trusted,
            RelationshipBondClassifier.Classify(relationship));

        relationship.ApplyDelta(RelationshipDimension.Fear, -1);
        Assert.Equal(
            RelationshipBondLevel.Trusted,
            RelationshipBondClassifier.Classify(relationship));

        relationship.ApplyDelta(RelationshipDimension.Loyalty, 60);
        Assert.Equal(
            RelationshipBondLevel.Loyal,
            RelationshipBondClassifier.Classify(relationship));
    }

    [Fact]
    public void ClassificationDoesNotMutateRelationshipValues()
    {
        var relationship = new RelationshipState("entity-1", "entity-2");
        relationship.ApplyDelta(RelationshipDimension.Familiarity, 35);
        relationship.ApplyDelta(RelationshipDimension.Trust, 55);
        relationship.ApplyDelta(RelationshipDimension.Fear, 5);
        relationship.ApplyDelta(RelationshipDimension.Loyalty, 12);

        var before = (
            relationship.Familiarity,
            relationship.Trust,
            relationship.Fear,
            relationship.Loyalty);

        RelationshipBondClassifier.Classify(relationship);

        Assert.Equal(before, (
            relationship.Familiarity,
            relationship.Trust,
            relationship.Fear,
            relationship.Loyalty));
    }

    [Theory]
    [InlineData(-1, 50, 75, 60, 20, 50)]
    [InlineData(20, 101, 75, 60, 20, 50)]
    [InlineData(20, 50, 49, 60, 20, 50)]
    [InlineData(20, 50, 75, 60, 50, 50)]
    [InlineData(20, 50, 75, double.NaN, 20, 50)]
    public void InvalidThresholdsAreRejected(
        double familiarity,
        double trusted,
        double loyalTrust,
        double loyalLoyalty,
        double maxLoyalFear,
        double fearful)
    {
        Assert.ThrowsAny<ArgumentException>(() => new RelationshipBondThresholds(
            familiarity,
            trusted,
            loyalTrust,
            loyalLoyalty,
            maxLoyalFear,
            fearful));
    }

    [Fact]
    public void ClassificationUsesSuppliedThresholdProfile()
    {
        var relationship = new RelationshipState("entity-1", "entity-2");
        relationship.ApplyDelta(RelationshipDimension.Familiarity, 5);
        relationship.ApplyDelta(RelationshipDimension.Trust, 10);
        var thresholds = new RelationshipBondThresholds(5, 10, 20, 15, 5, 25);

        Assert.Equal(
            RelationshipBondLevel.Trusted,
            RelationshipBondClassifier.Classify(relationship, thresholds));
    }
}
