namespace Simulation;

/// <summary>Derives stable individual trait variation from an entity's durable ID and species baseline.</summary>
public static class IndividualTemperament
{
    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;
    private const double Variation = 0.5;

    public static TemperamentProfile ForEntity(string entityId, TemperamentProfile speciesTemperament)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentNullException.ThrowIfNull(speciesTemperament);

        return new TemperamentProfile(
            Adjust(speciesTemperament.Boldness, entityId, "boldness"),
            Adjust(speciesTemperament.Aggression, entityId, "aggression"),
            Adjust(speciesTemperament.Fear, entityId, "fear"),
            Adjust(speciesTemperament.Curiosity, entityId, "curiosity"),
            Adjust(speciesTemperament.Sociability, entityId, "sociability"),
            Adjust(speciesTemperament.Territoriality, entityId, "territoriality"));
    }

    private static double Adjust(double baseline, string entityId, string trait)
    {
        var hash = FnvOffsetBasis;
        foreach (var character in entityId + "#" + trait)
        {
            hash = unchecked((hash ^ character) * FnvPrime);
        }

        var offset = ((hash % 1001) / 1000.0) - 0.5;
        return Math.Clamp(baseline + offset * Variation, 0, 1);
    }
}
