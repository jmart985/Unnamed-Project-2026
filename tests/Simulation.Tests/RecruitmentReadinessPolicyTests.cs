using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class RecruitmentReadinessPolicyTests
{
    [Fact]
    public void EvaluatesEveryBondLevelWithoutChangingRelationshipValues()
    {
        var world = new World();
        var recruiter = world.SpawnEntity("human", Vector3.Zero);
        var stranger = world.SpawnEntity("human", Vector3.One);
        var familiar = world.SpawnEntity("human", Vector3.UnitX);
        var trusted = world.SpawnEntity("human", Vector3.UnitY);
        var loyal = world.SpawnEntity("human", Vector3.UnitZ);
        var fearful = world.SpawnEntity("creature", Vector3.One);

        Assert.True(world.TryGetOrCreateRelationship(stranger.Id, recruiter.Id, out _));
        Assert.True(world.TryGetOrCreateRelationship(familiar.Id, recruiter.Id, out var familiarRelationship));
        familiarRelationship!.ApplyDelta(RelationshipDimension.Familiarity, 20);
        Assert.True(world.TryGetOrCreateRelationship(trusted.Id, recruiter.Id, out var trustedRelationship));
        trustedRelationship!.ApplyDelta(RelationshipDimension.Familiarity, 20);
        trustedRelationship.ApplyDelta(RelationshipDimension.Trust, 50);
        Assert.True(world.TryGetOrCreateRelationship(loyal.Id, recruiter.Id, out var loyalRelationship));
        loyalRelationship!.ApplyDelta(RelationshipDimension.Familiarity, 20);
        loyalRelationship.ApplyDelta(RelationshipDimension.Trust, 75);
        loyalRelationship.ApplyDelta(RelationshipDimension.Loyalty, 60);
        Assert.True(world.TryGetOrCreateRelationship(fearful.Id, recruiter.Id, out var fearfulRelationship));
        fearfulRelationship!.ApplyDelta(RelationshipDimension.Fear, 50);

        Assert.Equal(
            RecruitmentReadinessResult.Stranger,
            RecruitmentReadinessPolicy.Evaluate(world, stranger.Id, recruiter.Id));
        Assert.Equal(
            RecruitmentReadinessResult.Familiar,
            RecruitmentReadinessPolicy.Evaluate(world, familiar.Id, recruiter.Id));
        Assert.Equal(
            RecruitmentReadinessResult.Trusted,
            RecruitmentReadinessPolicy.Evaluate(world, trusted.Id, recruiter.Id));
        Assert.Equal(
            RecruitmentReadinessResult.Eligible,
            RecruitmentReadinessPolicy.Evaluate(world, loyal.Id, recruiter.Id));
        Assert.Equal(
            RecruitmentReadinessResult.Fearful,
            RecruitmentReadinessPolicy.Evaluate(world, fearful.Id, recruiter.Id));

        Assert.Equal(20, familiarRelationship.Familiarity);
        Assert.Equal(50, trustedRelationship.Trust);
        Assert.Equal(75, loyalRelationship.Trust);
        Assert.Equal(60, loyalRelationship.Loyalty);
        Assert.Equal(50, fearfulRelationship.Fear);
    }

    [Fact]
    public void MissingRelationshipIsIneligibleAndIsNotCreated()
    {
        var world = new World();
        var candidate = world.SpawnEntity("human", Vector3.Zero);
        var recruiter = world.SpawnEntity("human", Vector3.One);

        Assert.Equal(
            RecruitmentReadinessResult.MissingRelationship,
            RecruitmentReadinessPolicy.Evaluate(world, candidate.Id, recruiter.Id));
        Assert.Empty(world.Relationships);
    }

    [Fact]
    public void UsesConfiguredBondThresholds()
    {
        var world = new World();
        var candidate = world.SpawnEntity("human", Vector3.Zero);
        var recruiter = world.SpawnEntity("human", Vector3.One);
        Assert.True(world.TryGetOrCreateRelationship(candidate.Id, recruiter.Id, out var relationship));
        relationship!.ApplyDelta(RelationshipDimension.Familiarity, 10);
        relationship.ApplyDelta(RelationshipDimension.Trust, 20);
        relationship.ApplyDelta(RelationshipDimension.Loyalty, 20);

        var thresholds = new RelationshipBondThresholds(10, 20, 20, 20, 5, 25);

        Assert.Equal(
            RecruitmentReadinessResult.Eligible,
            RecruitmentReadinessPolicy.Evaluate(
                world,
                candidate.Id,
                recruiter.Id,
                thresholds));
    }

    [Fact]
    public void RecruitmentUsesProspectiveRecruitToRecruiterDirection()
    {
        var world = new World();
        var candidate = world.SpawnEntity("human", Vector3.Zero);
        var recruiter = world.SpawnEntity("human", Vector3.One);
        Assert.True(world.TryGetOrCreateRelationship(candidate.Id, recruiter.Id, out var candidateView));
        candidateView!.ApplyDelta(RelationshipDimension.Familiarity, 20);
        candidateView.ApplyDelta(RelationshipDimension.Trust, 75);
        candidateView.ApplyDelta(RelationshipDimension.Loyalty, 60);
        Assert.True(world.TryGetOrCreateRelationship(recruiter.Id, candidate.Id, out var recruiterView));
        recruiterView!.ApplyDelta(RelationshipDimension.Fear, 50);

        Assert.Equal(
            RecruitmentReadinessResult.Eligible,
            RecruitmentReadinessPolicy.Evaluate(world, candidate.Id, recruiter.Id));
        Assert.Equal(
            RecruitmentReadinessResult.Fearful,
            RecruitmentReadinessPolicy.Evaluate(world, recruiter.Id, candidate.Id));
    }
}
