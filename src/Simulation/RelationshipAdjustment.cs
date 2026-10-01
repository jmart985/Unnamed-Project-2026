namespace Simulation;

public sealed record RelationshipAdjustment(
    double FamiliarityDelta,
    double TrustDelta,
    double FearDelta,
    double LoyaltyDelta);

public enum RelationshipAdjustmentResult
{
    Applied,
    InvalidParticipant,
    InvalidDelta
}
