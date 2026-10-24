namespace Simulation;

/// <summary>
/// The reusable physical baseline shared by player, NPC, and creature entities.
/// Values are intentionally engine-independent so simulation systems can consume
/// them without depending on Godot nodes or presentation state.
/// </summary>
public sealed record CharacterStats
{
    public const double DefaultHealth = 100;
    public const double DefaultStamina = 100;
    public const double DefaultMovementSpeed = 3;
    /// <summary>Baseline carried load, in kilograms, before strength and endurance modifiers.</summary>
    public const int DefaultCarryCapacity = 10;
    public const double DefaultPerception = 1;
    public const double DefaultDefense = 0;
    public const double DefaultStrength = 1;
    public const double DefaultEndurance = 1;

    public CharacterStats(
        double health = DefaultHealth,
        double stamina = DefaultStamina,
        double movementSpeed = DefaultMovementSpeed,
        int carryCapacity = DefaultCarryCapacity,
        double perception = DefaultPerception,
        double defense = DefaultDefense,
        double strength = DefaultStrength,
        double endurance = DefaultEndurance)
    {
        ValidateFinitePositive(health, nameof(health));
        ValidateFinitePositive(stamina, nameof(stamina));
        ValidateFinitePositive(movementSpeed, nameof(movementSpeed));
        if (carryCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(carryCapacity), "Carry capacity cannot be negative.");
        }

        ValidateFiniteNonNegative(perception, nameof(perception));
        ValidateFiniteNonNegative(defense, nameof(defense));
        ValidateFinitePositive(strength, nameof(strength));
        ValidateFinitePositive(endurance, nameof(endurance));

        MaxHealth = health;
        MaxStamina = stamina;
        MovementSpeed = movementSpeed;
        CarryCapacity = carryCapacity;
        Perception = perception;
        Defense = defense;
        Strength = strength;
        Endurance = endurance;
    }

    public double MaxHealth { get; }
    public double MaxStamina { get; }
    public double MovementSpeed { get; }
    /// <summary>Baseline soft carried-load threshold in kilograms before strength is applied.</summary>
    public int CarryCapacity { get; }
    public double Perception { get; }
    public double Defense { get; }
    public double Strength { get; }
    public double Endurance { get; }

    /// <summary>Soft fatigue threshold in kilograms. This is not a hard inventory limit.</summary>
    public double EffectiveCarryCapacityKg => CarryCapacity * Strength;

    public int EffectiveCarryCapacity => checked((int)Math.Round(EffectiveCarryCapacityKg, MidpointRounding.AwayFromZero));

    public static CharacterStats Default { get; } = new();

    private static void ValidateFinitePositive(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and greater than zero.");
        }
    }

    private static void ValidateFiniteNonNegative(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and non-negative.");
        }
    }
}
