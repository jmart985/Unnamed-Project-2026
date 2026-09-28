namespace Simulation;

public sealed class NeedsTuning
{
    public NeedsTuning(
        double initialSatiety = 60,
        double drainPerSecond = 0.2,
        double hungryThreshold = 30,
        double nutritionPerUnit = 50)
    {
        ValidateRange(initialSatiety, 0, 100, nameof(initialSatiety));
        if (!double.IsFinite(drainPerSecond) || drainPerSecond < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(drainPerSecond), "Drain must be finite and nonnegative.");
        }

        ValidateRange(hungryThreshold, 0, 100, nameof(hungryThreshold));
        if (!double.IsFinite(nutritionPerUnit) || nutritionPerUnit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nutritionPerUnit), "Nutrition must be finite and positive.");
        }

        InitialSatiety = initialSatiety;
        DrainPerSecond = drainPerSecond;
        HungryThreshold = hungryThreshold;
        NutritionPerUnit = nutritionPerUnit;
    }

    public double InitialSatiety { get; }
    public double DrainPerSecond { get; }
    public double HungryThreshold { get; }
    public double NutritionPerUnit { get; }

    private static void ValidateRange(double value, double minimum, double maximum, string parameterName)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Value must be finite and within [{minimum}, {maximum}].");
        }
    }
}

public sealed class NeedsState
{
    private readonly NeedsTuning _tuning;

    public NeedsState(NeedsTuning tuning, double? initialSatiety = null)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        _tuning = tuning;
        Satiety = initialSatiety ?? tuning.InitialSatiety;
        if (!double.IsFinite(Satiety) || Satiety is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(initialSatiety), "Satiety must be finite and within [0, 100].");
        }
    }

    public double Satiety { get; private set; }
    public bool IsHungry => Satiety <= _tuning.HungryThreshold;
    public bool IsStarving => Satiety <= 0;
    public double NutritionPerUnit => _tuning.NutritionPerUnit;

    public void Advance(double simulationSeconds, double? drainPerSecondOverride = null)
    {
        if (!double.IsFinite(simulationSeconds) || simulationSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(simulationSeconds), "Simulation time must be finite and nonnegative.");
        }

        var drainPerSecond = drainPerSecondOverride ?? _tuning.DrainPerSecond;
        if (!double.IsFinite(drainPerSecond) || drainPerSecond < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(drainPerSecondOverride), "Drain must be finite and nonnegative.");
        }

        Satiety = Math.Clamp(Satiety - drainPerSecond * simulationSeconds, 0, 100);
    }

    public void AddNutrition(double nutrition)
    {
        if (!double.IsFinite(nutrition) || nutrition <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nutrition), "Nutrition must be finite and positive.");
        }

        Satiety = Math.Clamp(Satiety + nutrition, 0, 100);
    }
}
