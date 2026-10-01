namespace Simulation;

public enum RelationshipBondLevel
{
    Stranger,
    Familiar,
    Trusted,
    Loyal,
    Fearful
}

public sealed class RelationshipBondThresholds
{
    public static RelationshipBondThresholds ProvisionalDefault { get; } = new(
        familiarityForFamiliar: 20,
        trustForTrusted: 50,
        trustForLoyal: 75,
        loyaltyForLoyal: 60,
        maximumFearForLoyal: 20,
        fearForFearful: 50);

    public RelationshipBondThresholds(
        double familiarityForFamiliar,
        double trustForTrusted,
        double trustForLoyal,
        double loyaltyForLoyal,
        double maximumFearForLoyal,
        double fearForFearful)
    {
        ValidateBounded(familiarityForFamiliar, nameof(familiarityForFamiliar));
        ValidateBounded(trustForTrusted, nameof(trustForTrusted));
        ValidateBounded(trustForLoyal, nameof(trustForLoyal));
        ValidateBounded(loyaltyForLoyal, nameof(loyaltyForLoyal));
        ValidateBounded(maximumFearForLoyal, nameof(maximumFearForLoyal));
        ValidateBounded(fearForFearful, nameof(fearForFearful));

        if (trustForLoyal < trustForTrusted)
        {
            throw new ArgumentException(
                "The loyalty trust threshold cannot be below the trusted threshold.",
                nameof(trustForLoyal));
        }

        if (maximumFearForLoyal >= fearForFearful)
        {
            throw new ArgumentException(
                "The maximum fear for loyalty must be below the fearful threshold.",
                nameof(maximumFearForLoyal));
        }

        FamiliarityForFamiliar = familiarityForFamiliar;
        TrustForTrusted = trustForTrusted;
        TrustForLoyal = trustForLoyal;
        LoyaltyForLoyal = loyaltyForLoyal;
        MaximumFearForLoyal = maximumFearForLoyal;
        FearForFearful = fearForFearful;
    }

    public double FamiliarityForFamiliar { get; }
    public double TrustForTrusted { get; }
    public double TrustForLoyal { get; }
    public double LoyaltyForLoyal { get; }
    public double MaximumFearForLoyal { get; }
    public double FearForFearful { get; }

    private static void ValidateBounded(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Relationship thresholds must be finite and within [0, 100].");
        }
    }
}

public static class RelationshipBondClassifier
{
    public static RelationshipBondLevel Classify(
        RelationshipState relationship,
        RelationshipBondThresholds? thresholds = null)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        thresholds ??= RelationshipBondThresholds.ProvisionalDefault;

        if (relationship.Fear >= thresholds.FearForFearful)
        {
            return RelationshipBondLevel.Fearful;
        }

        if (relationship.Familiarity >= thresholds.FamiliarityForFamiliar
            && relationship.Trust >= thresholds.TrustForLoyal
            && relationship.Loyalty >= thresholds.LoyaltyForLoyal
            && relationship.Fear <= thresholds.MaximumFearForLoyal)
        {
            return RelationshipBondLevel.Loyal;
        }

        if (relationship.Familiarity >= thresholds.FamiliarityForFamiliar
            && relationship.Trust >= thresholds.TrustForTrusted)
        {
            return RelationshipBondLevel.Trusted;
        }

        return relationship.Familiarity >= thresholds.FamiliarityForFamiliar
            ? RelationshipBondLevel.Familiar
            : RelationshipBondLevel.Stranger;
    }
}
