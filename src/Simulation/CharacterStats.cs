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
    public const int DefaultCarryCapacity = 10;
    public const double DefaultPerception = 1;
    public const double DefaultDefense = 0;

    public CharacterStats(
        double health = DefaultHealth,
        double stamina = DefaultStamina,
        double movementSpeed = DefaultMovementSpeed,
        int carryCapacity = DefaultCarryCapacity,
        double perception = DefaultPerception,
        double defense = DefaultDefense)
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

        MaxHealth = health;
        MaxStamina = stamina;
        MovementSpeed = movementSpeed;
        CarryCapacity = carryCapacity;
        Perception = perception;
        Defense = defense;
    }

    public double MaxHealth { get; }
    public double MaxStamina { get; }
    public double MovementSpeed { get; }
    public int CarryCapacity { get; }
    public double Perception { get; }
    public double Defense { get; }

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
