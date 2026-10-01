namespace Simulation;

public enum RelationshipDimension
{
    Familiarity,
    Trust,
    Fear,
    Loyalty
}

public enum RelationshipUpdateResult
{
    Applied,
    InvalidDimension,
    InvalidDelta
}

public sealed class RelationshipState
{
    public RelationshipState(string observerEntityId, string targetEntityId)
    {
        if (string.IsNullOrWhiteSpace(observerEntityId))
        {
            throw new ArgumentException("An observer entity ID is required.", nameof(observerEntityId));
        }

        if (string.IsNullOrWhiteSpace(targetEntityId))
        {
            throw new ArgumentException("A target entity ID is required.", nameof(targetEntityId));
        }

        if (string.Equals(observerEntityId, targetEntityId, StringComparison.Ordinal))
        {
            throw new ArgumentException("An entity cannot have a relationship with itself.", nameof(targetEntityId));
        }

        ObserverEntityId = observerEntityId;
        TargetEntityId = targetEntityId;
    }

    public string ObserverEntityId { get; }
    public string TargetEntityId { get; }
    public double Familiarity { get; private set; }
    public double Trust { get; private set; }
    public double Fear { get; private set; }
    public double Loyalty { get; private set; }

    public RelationshipUpdateResult ApplyDelta(RelationshipDimension dimension, double delta)
    {
        if (!Enum.IsDefined(dimension))
        {
            return RelationshipUpdateResult.InvalidDimension;
        }

        if (!double.IsFinite(delta))
        {
            return RelationshipUpdateResult.InvalidDelta;
        }

        var currentValue = GetValue(dimension);
        var updatedValue = delta >= 100 - currentValue
            ? 100
            : delta <= -currentValue
                ? 0
                : currentValue + delta;

        SetValue(dimension, updatedValue);
        return RelationshipUpdateResult.Applied;
    }

    private double GetValue(RelationshipDimension dimension)
    {
        return dimension switch
        {
            RelationshipDimension.Familiarity => Familiarity,
            RelationshipDimension.Trust => Trust,
            RelationshipDimension.Fear => Fear,
            RelationshipDimension.Loyalty => Loyalty,
            _ => throw new ArgumentOutOfRangeException(nameof(dimension))
        };
    }

    private void SetValue(RelationshipDimension dimension, double value)
    {
        switch (dimension)
        {
            case RelationshipDimension.Familiarity:
                Familiarity = value;
                break;
            case RelationshipDimension.Trust:
                Trust = value;
                break;
            case RelationshipDimension.Fear:
                Fear = value;
                break;
            case RelationshipDimension.Loyalty:
                Loyalty = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(dimension));
        }
    }
}
