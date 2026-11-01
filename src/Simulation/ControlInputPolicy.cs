namespace Simulation;

[Flags]
public enum ControlContext
{
    None = 0, Gameplay = 1, Build = 2, Wheel = 4, Modal = 8, TextEntry = 16, Rebinding = 32,
    Play = Gameplay | Build, All = Gameplay | Build | Wheel | Modal | TextEntry | Rebinding
}

/// Input ownership is independent of Godot events and global polling.
public static class ControlInputPolicy
{
    public static ControlContext Resolve(bool rebinding, bool textEntry, bool modal, bool wheel, bool building)
        => rebinding ? ControlContext.Rebinding : textEntry ? ControlContext.TextEntry
            : modal ? ControlContext.Modal : wheel ? ControlContext.Wheel
            : building ? ControlContext.Build : ControlContext.Gameplay;

    public static bool Accepts(ControlContext owner, ControlContext allowed)
        => owner != ControlContext.None && (owner & allowed) == owner;
}

public enum HoldTapSignal { None, Tap, HoldStarted, HoldEnded }

/// Uses unscaled, monotonic elapsed time. A completed hold can never also emit Tap.
public sealed class HoldTapGesture
{
    private double _started;
    private double _lastTime;
    private bool _pressed;
    private bool _holdFired;
    private bool _toggle;

    public double HoldSeconds { get; }
    public bool IsActive { get; private set; }
    public bool IsPressed => _pressed;

    public HoldTapGesture(double holdSeconds = 0.30)
    {
        if (!double.IsFinite(holdSeconds) || holdSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(holdSeconds));
        HoldSeconds = holdSeconds;
    }

    public void Press(double time, bool toggle = false)
    {
        CheckTime(time);
        if (_pressed) return;
        _started = time;
        _pressed = true;
        _holdFired = false;
        _toggle = toggle;
    }

    public HoldTapSignal Advance(double time)
    {
        CheckTime(time);
        if (!_pressed || _holdFired || time - _started + 1e-12 < HoldSeconds) return HoldTapSignal.None;
        _holdFired = true;
        IsActive = _toggle ? !IsActive : true;
        return HoldTapSignal.HoldStarted;
    }

    public HoldTapSignal Release(double time)
    {
        if (!_pressed) { CheckTime(time); return HoldTapSignal.None; }
        Advance(time);
        _pressed = false;
        if (!_holdFired) return HoldTapSignal.Tap;
        if (_toggle) return HoldTapSignal.None;
        IsActive = false;
        return HoldTapSignal.HoldEnded;
    }

    public void Cancel()
    {
        _pressed = false;
        _holdFired = false;
        IsActive = false;
    }

    private void CheckTime(double time)
    {
        if (!double.IsFinite(time) || time < 0 || time < _lastTime)
            throw new ArgumentOutOfRangeException(nameof(time), "Gesture time must be finite and monotonic.");
        _lastTime = time;
    }
}

/// Multiple UI owners cannot accidentally resume an already manually paused world.
public sealed class ControlPauseState
{
    private readonly HashSet<string> _owners = new(StringComparer.Ordinal);
    public bool ManuallyPaused { get; private set; }
    public bool IsPaused => ManuallyPaused || _owners.Count > 0;
    public void SetManual(bool paused) => ManuallyPaused = paused;
    public void SetOwner(string owner, bool paused)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if (paused) _owners.Add(owner); else _owners.Remove(owner);
    }
    public void Reset() { _owners.Clear(); ManuallyPaused = false; }
}

