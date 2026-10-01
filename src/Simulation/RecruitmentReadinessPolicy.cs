namespace Simulation;

public enum RecruitmentReadinessResult
{
    Eligible,
    MissingRelationship,
    Stranger,
    Familiar,
    Trusted,
    Fearful
}

public static class RecruitmentReadinessPolicy
{
    public static RecruitmentReadinessResult Evaluate(
        World world,
        string? prospectiveRecruitEntityId,
        string? recruiterEntityId,
        RelationshipBondThresholds? thresholds = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!world.TryGetRelationship(
            prospectiveRecruitEntityId,
            recruiterEntityId,
            out var relationship)
            || relationship is null)
        {
            return RecruitmentReadinessResult.MissingRelationship;
        }

        return RelationshipBondClassifier.Classify(relationship, thresholds) switch
        {
            RelationshipBondLevel.Loyal => RecruitmentReadinessResult.Eligible,
            RelationshipBondLevel.Stranger => RecruitmentReadinessResult.Stranger,
            RelationshipBondLevel.Familiar => RecruitmentReadinessResult.Familiar,
            RelationshipBondLevel.Trusted => RecruitmentReadinessResult.Trusted,
            RelationshipBondLevel.Fearful => RecruitmentReadinessResult.Fearful,
            _ => throw new ArgumentOutOfRangeException(nameof(thresholds))
        };
    }
}
