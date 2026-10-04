namespace Simulation;

public enum CollectUseIntent { None, Collect, Use }

/// <summary>One short release or one deliberate hold; cancellation never commits an action.</summary>
public sealed class CollectUseGesture
{
    public const double HoldSeconds = 0.6;
    public bool IsPressed { get; private set; }
    public bool Used { get; private set; }
    public double Elapsed { get; private set; }
    public void Press()
    {
        if (IsPressed) return;
        IsPressed = true; Used = false; Elapsed = 0;
    }
    public CollectUseIntent Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!IsPressed || Used) return CollectUseIntent.None;
        Elapsed += seconds;
        if (Elapsed < HoldSeconds) return CollectUseIntent.None;
        Used = true;
        return CollectUseIntent.Use;
    }
    public CollectUseIntent Release()
    {
        var result = IsPressed && !Used ? CollectUseIntent.Collect : CollectUseIntent.None;
        Cancel();
        return result;
    }
    public void Cancel() { IsPressed = false; Used = false; Elapsed = 0; }
}
