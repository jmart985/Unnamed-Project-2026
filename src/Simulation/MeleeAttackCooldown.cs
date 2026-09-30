namespace Simulation;

public sealed class MeleeAttackCooldown
{
    public MeleeAttackCooldown(double duration)
    {
        if (!double.IsFinite(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
        Duration = duration;
    }
    public double Duration { get; }
    public double Remaining { get; private set; }
    public bool IsReady => Remaining <= 0;
    public bool TryStart()
    {
        if (!IsReady) return false;
        Remaining = Duration; return true;
    }
    public void Advance(double elapsed)
    {
        if (!double.IsFinite(elapsed) || elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
        Remaining = Math.Max(0, Remaining - elapsed);
    }
}
