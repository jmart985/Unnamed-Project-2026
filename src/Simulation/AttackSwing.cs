namespace Simulation;

public enum AttackSwingPhase
{
    Idle,
    WindUp,
    Strike,
    Recovery
}

/// <summary>
/// Pure timing model for a single melee swing: wind-up, strike, recovery. The strike moment is reported exactly
/// once so combat resolution (reach validation, damage to a body part) can hook in without reading animation state.
/// </summary>
public sealed class AttackSwing
{
    private const float DefaultWindUpSeconds = 0.12f;
    private const float DefaultStrikeSeconds = 0.08f;
    private const float DefaultRecoverySeconds = 0.18f;

    public AttackSwing(float windUpSeconds = 0.12f, float strikeSeconds = 0.08f, float recoverySeconds = 0.18f)
    {
        if (!float.IsFinite(windUpSeconds) || windUpSeconds <= 0
            || !float.IsFinite(strikeSeconds) || strikeSeconds <= 0
            || !float.IsFinite(recoverySeconds) || recoverySeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windUpSeconds), "Swing phases must be positive.");
        }

        WindUpSeconds = windUpSeconds;
        StrikeSeconds = strikeSeconds;
        RecoverySeconds = recoverySeconds;
    }

    private float _elapsed;
    private bool _strikeReported;

    public float WindUpSeconds { get; }
    public float StrikeSeconds { get; }
    public float RecoverySeconds { get; }
    public float TotalSeconds => WindUpSeconds + StrikeSeconds + RecoverySeconds;
    public bool IsActive { get; private set; }

    public static AttackSwing ForTotalDuration(float totalSeconds)
    {
        if (!float.IsFinite(totalSeconds) || totalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalSeconds), "Swing duration must be finite and positive.");
        }

        var defaultTotalSeconds = DefaultWindUpSeconds + DefaultStrikeSeconds + DefaultRecoverySeconds;
        var windUpSeconds = totalSeconds * (DefaultWindUpSeconds / defaultTotalSeconds);
        var strikeSeconds = totalSeconds * (DefaultStrikeSeconds / defaultTotalSeconds);
        var recoverySeconds = totalSeconds - windUpSeconds - strikeSeconds;
        return new AttackSwing(windUpSeconds, strikeSeconds, recoverySeconds);
    }

    public AttackSwingPhase Phase => !IsActive
        ? AttackSwingPhase.Idle
        : _elapsed < WindUpSeconds
            ? AttackSwingPhase.WindUp
            : _elapsed < WindUpSeconds + StrikeSeconds
                ? AttackSwingPhase.Strike
                : AttackSwingPhase.Recovery;

    /// <summary>Arm raise amount: 0 at rest, 1 fully wound up, -1 at full follow-through, back to 0 on recovery.</summary>
    public float Pose
    {
        get
        {
            if (!IsActive) return 0;
            if (_elapsed < WindUpSeconds) return _elapsed / WindUpSeconds;
            var strikeEnd = WindUpSeconds + StrikeSeconds;
            if (_elapsed < strikeEnd) return 1 - 2 * ((_elapsed - WindUpSeconds) / StrikeSeconds);
            return -(1 - (_elapsed - strikeEnd) / RecoverySeconds);
        }
    }

    public bool Begin()
    {
        if (IsActive) return false;
        IsActive = true;
        _elapsed = 0;
        _strikeReported = false;
        return true;
    }

    /// <summary>Advances the swing; returns true on the single step in which the strike phase is first entered.</summary>
    public bool Advance(float deltaSeconds)
    {
        if (!IsActive || deltaSeconds <= 0 || !float.IsFinite(deltaSeconds)) return false;
        _elapsed += deltaSeconds;
        var struck = false;
        if (!_strikeReported && _elapsed >= WindUpSeconds)
        {
            _strikeReported = true;
            struck = true;
        }

        if (_elapsed >= TotalSeconds)
        {
            IsActive = false;
            _elapsed = 0;
        }

        return struck;
    }
}

/// <summary>Maps head pitch (radians, positive = looking up) to the body part a strike is aimed at.</summary>
public static class CombatAim
{
    public const float HighPitch = 0.25f;
    public const float LowPitch = -0.3f;

    public static string PartForPitch(float pitchRadians, bool leftHand)
    {
        if (pitchRadians >= HighPitch) return "Head";
        if (pitchRadians <= LowPitch) return leftHand ? "LeftLeg" : "RightLeg";
        return "Torso";
    }
}
