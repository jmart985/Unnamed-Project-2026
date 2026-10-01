namespace Simulation;

/// Calculates exertion from carried load and endurance using engine-independent rules.
public static class LoadFatigue
{
    public static double PerSecond(double carriedUnits, CharacterStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        if (!double.IsFinite(carriedUnits) || carriedUnits < 0)
            throw new ArgumentOutOfRangeException(nameof(carriedUnits));

        var normalizedLoad = carriedUnits / Math.Max(1, stats.EffectiveCarryCapacity);
        return normalizedLoad <= 1
            ? normalizedLoad / stats.Endurance
            : (1 + (normalizedLoad - 1) * 2) / stats.Endurance;
    }
}
