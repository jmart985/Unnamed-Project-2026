namespace Simulation;

public sealed class NeedsTuning
{
    public NeedsTuning(
        double initialSatiety = 60,
        double satietyDrainPerSecond = 0.00001, // Calibrated for days (approx 1% per 28 hours)
        double hungryThreshold = 30,
        double nutritionPerUnit = 50,
        double initialHydration = 60,
        double hydrationDrainPerSecond = 0.00002, // Faster than food
        double thirstyThreshold = 30,
        double hydrationPerUnit = 50,
        double initialFatigue = 0,
        double fatigueAccumulationPerSecond = 0.00001, // Passive sleep pressure
        double exhaustedThreshold = 80,
        double bedRecoveryRate = 0.0005) // Recovery while sleeping in bed
    {
        ValidateRange(initialSatiety, 0, 100, nameof(initialSatiety));
        if (!double.IsFinite(satietyDrainPerSecond) || satietyDrainPerSecond < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(satietyDrainPerSecond), "Drain must be finite and nonnegative.");
        }

        ValidateRange(hungryThreshold, 0, 100, nameof(hungryThreshold));
        if (!double.IsFinite(nutritionPerUnit) || nutritionPerUnit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nutritionPerUnit), "Nutrition must be finite and positive.");
        }

        ValidateRange(initialHydration, 0, 100, nameof(initialHydration));
        if (!double.IsFinite(hydrationDrainPerSecond) || hydrationDrainPerSecond < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hydrationDrainPerSecond), "Drain must be finite and nonnegative.");
        }

        ValidateRange(thirstyThreshold, 0, 100, nameof(thirstyThreshold));
        if (!double.IsFinite(hydrationPerUnit) || hydrationPerUnit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hydrationPerUnit), "Hydration must be finite and positive.");
        }

        ValidateRange(initialFatigue, 0, 100, nameof(initialFatigue));
        if (!double.IsFinite(fatigueAccumulationPerSecond) || fatigueAccumulationPerSecond < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fatigueAccumulationPerSecond), "Accumulation must be finite and nonnegative.");
        }

        ValidateRange(exhaustedThreshold, 0, 100, nameof(exhaustedThreshold));
        if (!double.IsFinite(bedRecoveryRate) || bedRecoveryRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bedRecoveryRate), "Recovery rate must be finite and positive.");
        }

        InitialSatiety = initialSatiety;
        SatietyDrainPerSecond = satietyDrainPerSecond;
        HungryThreshold = hungryThreshold;
        NutritionPerUnit = nutritionPerUnit;
        InitialHydration = initialHydration;
        HydrationDrainPerSecond = hydrationDrainPerSecond;
        ThirstyThreshold = thirstyThreshold;
        HydrationPerUnit = hydrationPerUnit;
        InitialFatigue = initialFatigue;
        FatigueAccumulationPerSecond = fatigueAccumulationPerSecond;
        ExhaustedThreshold = exhaustedThreshold;
        BedRecoveryRate = bedRecoveryRate;
    }

    public double InitialSatiety { get; }
    public double SatietyDrainPerSecond { get; }
    public double HungryThreshold { get; }
    public double NutritionPerUnit { get; }
    public double InitialHydration { get; }
    public double HydrationDrainPerSecond { get; }
    public double ThirstyThreshold { get; }
    public double HydrationPerUnit { get; }
    public double InitialFatigue { get; }
    public double FatigueAccumulationPerSecond { get; }
    public double ExhaustedThreshold { get; }
    public double BedRecoveryRate { get; }

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

    public NeedsState(NeedsTuning tuning, double? initialSatiety = null, double? initialHydration = null, double? initialFatigue = null)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        _tuning = tuning;
        Satiety = initialSatiety ?? tuning.InitialSatiety;
        Hydration = initialHydration ?? tuning.InitialHydration;
        Fatigue = initialFatigue ?? tuning.InitialFatigue;

        if (!double.IsFinite(Satiety) || Satiety is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(initialSatiety), "Satiety must be finite and within [0, 100].");
        }
        if (!double.IsFinite(Hydration) || Hydration is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(initialHydration), "Hydration must be finite and within [0, 100].");
        }
        if (!double.IsFinite(Fatigue) || Fatigue is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(initialFatigue), "Fatigue must be finite and within [0, 100].");
        }
    }

    public double Satiety { get; private set; }
    public double Hydration { get; private set; }
    public double Fatigue { get; private set; }
    public double DeprivationTime { get; private set; }

    public bool IsHungry => Satiety <= _tuning.HungryThreshold;
    public bool IsThirsty => Hydration <= _tuning.ThirstyThreshold;
    public bool IsStarving => Satiety <= 0;
    public bool IsDehydrated => Hydration <= 0;
    public bool IsExhausted => Fatigue >= _tuning.ExhaustedThreshold;

    public double NutritionPerUnit => _tuning.NutritionPerUnit;
    public double HydrationPerUnit => _tuning.HydrationPerUnit;
    internal double PassiveFatigueAccumulationPerSecond => _tuning.FatigueAccumulationPerSecond;

    public void AdvanceWithLoad(
        double simulationSeconds,
        double carriedWeightKg,
        CharacterStats stats,
        bool isSleeping = false,
        double? satietyOverride = null,
        double? hydrationOverride = null)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var loadFatigue = LoadFatigue.PerSecond(carriedWeightKg, stats);
        if (isSleeping)
        {
            Advance(
                simulationSeconds,
                isSleeping: true,
                satietyOverride: satietyOverride,
                hydrationOverride: hydrationOverride);
            return;
        }

        Advance(
            simulationSeconds,
            satietyOverride: satietyOverride,
            hydrationOverride: hydrationOverride,
            fatigueOverride: PassiveFatigueAccumulationPerSecond + loadFatigue);
    }

    public void Advance(double simulationSeconds, bool isSleeping = false, double? satietyOverride = null, double? hydrationOverride = null, double? fatigueOverride = null)
    {
        if (!double.IsFinite(simulationSeconds) || simulationSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(simulationSeconds), "Simulation time must be finite and nonnegative.");
        }

        // 1. Satiety and Hydration
        var sDrain = satietyOverride ?? _tuning.SatietyDrainPerSecond;
        var hDrain = hydrationOverride ?? _tuning.HydrationDrainPerSecond;
        
        Satiety = Math.Clamp(Satiety - sDrain * simulationSeconds, 0, 100);
        Hydration = Math.Clamp(Hydration - hDrain * simulationSeconds, 0, 100);

        // 2. Fatigue
        if (isSleeping)
        {
            Fatigue = Math.Clamp(Fatigue - _tuning.BedRecoveryRate * simulationSeconds, 0, 100);
        }
        else
        {
            var fAccum = fatigueOverride ?? _tuning.FatigueAccumulationPerSecond;
            Fatigue = Math.Clamp(Fatigue + fAccum * simulationSeconds, 0, 100);
        }

        // 3. Deprivation Clock
        // If either core need is at 0, we start counting deprivation time.
        if (IsStarving || IsDehydrated)
        {
            DeprivationTime += simulationSeconds;
        }
        else
        {
            // Recovery of deprivation time (e.g., once you eat, the physical damage doesn't vanish instantly)
            DeprivationTime = Math.Max(0, DeprivationTime - simulationSeconds * 0.1);
        }
    }

    public void AddNutrition(double nutrition)
    {
        if (!double.IsFinite(nutrition) || nutrition <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nutrition), "Nutrition must be finite and positive.");
        }
        Satiety = Math.Clamp(Satiety + nutrition, 0, 100);
    }

    public void AddHydration(double hydration)
    {
        if (!double.IsFinite(hydration) || hydration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hydration), "Hydration must be finite and positive.");
        }
        Hydration = Math.Clamp(Hydration + hydration, 0, 100);
    }
}
