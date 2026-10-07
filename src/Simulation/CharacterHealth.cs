namespace Simulation;

public sealed record HealthPartSnapshot(string Name, double MaximumHealth, double CurrentHealth, bool RequiredForConsciousness);

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
    private bool _isDead;
    private double _pendingDeathSeconds;

    public CharacterHealth(IEnumerable<BodyPartHealth> parts, double headDeathDelaySeconds = 1)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (!double.IsFinite(headDeathDelaySeconds) || headDeathDelaySeconds <= 0) throw new ArgumentOutOfRangeException(nameof(headDeathDelaySeconds));
        HeadDeathDelaySeconds = headDeathDelaySeconds;
        var materialized = parts.ToArray();
        if (materialized.Length == 0) throw new ArgumentException("At least one body part is required.", nameof(parts));
        if (materialized.Select(part => part.Name).Distinct(StringComparer.Ordinal).Count() != materialized.Length)
            throw new ArgumentException("Body-part names must be unique.", nameof(parts));
        _parts = Array.AsReadOnly(materialized);
    }

    public IReadOnlyList<BodyPartHealth> Parts => _parts;
    public bool IsIncapacitated => _isDead || _parts.Any(part => part.RequiredForConsciousness && part.IsDestroyed);
    public bool IsDead => _isDead;
    public double HeadDeathDelaySeconds { get; }
    public double PendingDeathSeconds => _pendingDeathSeconds;

    internal IReadOnlyList<HealthPartSnapshot> Capture()
    {
        return _parts.Select(part => new HealthPartSnapshot(
            part.Name, part.MaximumHealth, part.CurrentHealth, part.RequiredForConsciousness)).ToArray();
    }

    internal void Restore(IReadOnlyList<HealthPartSnapshot> parts, double pendingDeathSeconds, bool isDead)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (!double.IsFinite(pendingDeathSeconds) || pendingDeathSeconds < 0 || pendingDeathSeconds > HeadDeathDelaySeconds)
            throw new ArgumentOutOfRangeException(nameof(pendingDeathSeconds));
        if (isDead && pendingDeathSeconds > 0)
            throw new ArgumentException("Dead health cannot retain a pending death timer.", nameof(isDead));
        if (parts.Count != _parts.Count)
            throw new ArgumentException("The saved body-part count does not match the character.", nameof(parts));
        for (var index = 0; index < _parts.Count; index++)
        {
            var saved = parts[index];
            var target = _parts[index];
            if (saved is null
                || !string.Equals(saved.Name, target.Name, StringComparison.Ordinal)
                || saved.MaximumHealth != target.MaximumHealth
                || saved.RequiredForConsciousness != target.RequiredForConsciousness
                || !double.IsFinite(saved.CurrentHealth)
                || saved.CurrentHealth < 0 || saved.CurrentHealth > target.MaximumHealth)
                throw new ArgumentException("Saved health for a body part is invalid.", nameof(parts));
            if (saved.CurrentHealth < target.CurrentHealth)
                target.ApplyDamage(target.CurrentHealth - saved.CurrentHealth);
            else if (saved.CurrentHealth > target.CurrentHealth)
                target.Heal(saved.CurrentHealth - target.CurrentHealth);
        }
        _pendingDeathSeconds = pendingDeathSeconds;
        _isDead = isDead;
    }

    public bool TryGetPart(string? name, out BodyPartHealth? part)
    {
        part = _parts.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));
        return part is not null;
    }

    public bool TryDamage(string? name, double amount)
    {
        return TryDamage(name, amount, out _);
    }

    public bool TryDamage(string? name, double amount, out double appliedDamage)
    {
        appliedDamage = 0;
        if (_isDead || !TryGetPart(name, out var part) || part is null)
        {
            return false;
        }

        var healthBeforeDamage = part.CurrentHealth;
        part.ApplyDamage(amount);
        appliedDamage = healthBeforeDamage - part.CurrentHealth;
        if (appliedDamage > 0)
        {
            if (string.Equals(name, "Head", StringComparison.Ordinal) && part.IsDestroyed)
            {
                _pendingDeathSeconds = HeadDeathDelaySeconds;
            }

            if (string.Equals(name, "Torso", StringComparison.Ordinal) && part.IsDestroyed)
            {
                _isDead = true;
            }
        }

        return true;
    }

    public bool TryHeal(string? name, double amount)
    {
        if (_isDead) return false;
        var healed = TryGetPart(name, out var part) && part is not null && part.Heal(amount) >= 0;
        if (healed && string.Equals(name, "Head", StringComparison.Ordinal) && part is not null && !part.IsDestroyed) _pendingDeathSeconds = 0;
        return healed;
    }

    public void AdvanceTime(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (_isDead || _pendingDeathSeconds <= 0) return;
        _pendingDeathSeconds = Math.Max(0, _pendingDeathSeconds - seconds);
        if (_pendingDeathSeconds == 0) _isDead = true;
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


