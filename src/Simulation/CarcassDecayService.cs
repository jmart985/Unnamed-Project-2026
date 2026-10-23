namespace Simulation;

/// <summary>Advances perishable food sources using simulation time and removes expired carcasses.</summary>
public sealed class CarcassDecayService(World world)
{
    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));

    public IReadOnlyList<string> Advance(double simulationSeconds)
    {
        if (!double.IsFinite(simulationSeconds) || simulationSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(simulationSeconds), "Elapsed simulation time must be finite and nonnegative.");

        if (simulationSeconds == 0) return Array.Empty<string>();

        var expired = new List<string>();
        foreach (var source in _world.FoodSources.ToArray())
        {
            if (source.FreshnessSecondsRemaining is null || !source.AdvanceFreshness(simulationSeconds))
                continue;
            if (_world.TryRemoveFoodSource(source.Id))
                expired.Add(source.Id);
        }

        return expired;
    }
}
