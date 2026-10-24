namespace Simulation;

/// Calculates fatigue points per simulated second from carried kilograms, strength, and endurance.
public static class LoadFatigue
{
    // At the baseline load threshold, carrying fatigue matches the prototype's bed recovery rate.
    // This keeps a light tool from exhausting an entity in minutes while preserving the load curve.
    public const double FatiguePerSecondAtCapacity = 0.0005;

    public static double PerSecond(double carriedWeightKg, CharacterStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        if (!double.IsFinite(carriedWeightKg) || carriedWeightKg < 0)
            throw new ArgumentOutOfRangeException(nameof(carriedWeightKg));

        var normalizedLoad = carriedWeightKg / Math.Max(1, stats.EffectiveCarryCapacityKg);
        return normalizedLoad <= 1
            ? normalizedLoad * FatiguePerSecondAtCapacity / stats.Endurance
            : (1 + (normalizedLoad - 1) * 2) * FatiguePerSecondAtCapacity / stats.Endurance;
    }
}
