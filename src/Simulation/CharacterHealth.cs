namespace Simulation;

/// <summary>Mutable health for one named body part.</summary>
public sealed class BodyPartHealth
{
    public BodyPartHealth(string name, double maximumHealth, bool requiredForConsciousness = true)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A body-part name is required.", nameof(name));
        if (!double.IsFinite(maximumHealth) || maximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumHealth), "Maximum health must be finite and greater than zero.");
        Name = name;
        MaximumHealth = maximumHealth;
        CurrentHealth = maximumHealth;
        RequiredForConsciousness = requiredForConsciousness;
    }

    public string Name { get; }
    public double MaximumHealth { get; }
    public double CurrentHealth { get; private set; }
    public bool RequiredForConsciousness { get; }
    public bool IsDestroyed => CurrentHealth <= 0;

    public double ApplyDamage(double amount)
    {
        ValidateAmount(amount);
        var applied = Math.Min(amount, CurrentHealth);
        CurrentHealth -= applied;
        return applied;
    }

    public double Heal(double amount)
    {
        ValidateAmount(amount);
        var restored = Math.Min(amount, MaximumHealth - CurrentHealth);
        CurrentHealth += restored;
        return restored;
    }

    private static void ValidateAmount(double amount)
    {
        if (!double.IsFinite(amount) || amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be finite and non-negative.");
    }
}

/// <summary>Body-part health aggregate shared by characters and creatures.</summary>
public sealed class CharacterHealth
{
    private readonly IReadOnlyList<BodyPartHealth> _parts;

    public CharacterHealth(IEnumerable<BodyPartHealth> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var materialized = parts.ToArray();
        if (materialized.Length == 0) throw new ArgumentException("At least one body part is required.", nameof(parts));
        if (materialized.Select(part => part.Name).Distinct(StringComparer.Ordinal).Count() != materialized.Length)
            throw new ArgumentException("Body-part names must be unique.", nameof(parts));
        _parts = Array.AsReadOnly(materialized);
    }

    public IReadOnlyList<BodyPartHealth> Parts => _parts;
    public bool IsIncapacitated => _parts.Any(part => part.RequiredForConsciousness && part.IsDestroyed);

    public bool TryGetPart(string? name, out BodyPartHealth? part)
    {
        part = _parts.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));
        return part is not null;
    }

    public bool TryDamage(string? name, double amount)
    {
        return TryGetPart(name, out var part) && part is not null && part.ApplyDamage(amount) >= 0;
    }

    public bool TryHeal(string? name, double amount)
    {
        return TryGetPart(name, out var part) && part is not null && part.Heal(amount) >= 0;
    }

    public static CharacterHealth CreateHumanoid(double maximumHealth)
    {
        if (!double.IsFinite(maximumHealth) || maximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumHealth));
        var partHealth = maximumHealth / 4;
        return new CharacterHealth([
            new BodyPartHealth("Head", partHealth),
            new BodyPartHealth("Torso", maximumHealth / 2),
            new BodyPartHealth("LeftArm", partHealth / 2, false),
            new BodyPartHealth("RightArm", partHealth / 2, false),
            new BodyPartHealth("LeftLeg", partHealth / 2, false),
            new BodyPartHealth("RightLeg", partHealth / 2, false)]);
    }
}
