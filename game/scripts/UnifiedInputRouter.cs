using Godot;
using Simulation;

namespace UnnamedProject2026;

public sealed record ControlIntent(string Action, bool Pressed);

/// Owns physical input, context filtering, modifier precedence and neutral rearming.
public sealed class UnifiedInputRouter
{
    private readonly UnifiedControlSettings _settings;
    private readonly Dictionary<string, float> _physical = new(StringComparer.Ordinal);
    private readonly HashSet<string> _blocked = new(StringComparer.Ordinal);
    private readonly HashSet<string> _axisDown = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Pending> _pending = new(StringComparer.Ordinal);
    private readonly HashSet<string> _virtualBlocked = new(StringComparer.Ordinal);
    private string? _wheelToken;
    private string? _wheelAction;
    private string? _activeWheelOpener;
    private double _time;
    public int ActiveDevice { get; private set; } = -1;
    public bool ControllerLast { get; private set; }
    public ControlPreferences Preferences => _settings.Preferences;
    public ControlContext Context { get; private set; } = ControlContext.Gameplay;
    public event Action<ControlIntent>? Intent;
    public event Action<Vector2>? PointerLook;
    public event Action<Vector2>? WheelPointer;
    private sealed record Pending(HoldTapGesture Gesture, string? Tap, string? Hold);

    public UnifiedInputRouter(UnifiedControlSettings settings) => _settings = settings;

    public void SetContext(ControlContext context, string? wheelOpener = null)
    {
        _activeWheelOpener = context == ControlContext.Wheel ? wheelOpener : null;
        if (context == Context) return;
        var modalTransition = context is ControlContext.Modal or ControlContext.TextEntry or ControlContext.Rebinding
            || Context is ControlContext.Modal or ControlContext.TextEntry or ControlContext.Rebinding;
        foreach (var pending in _pending.Values) pending.Gesture.Cancel();
        _pending.Clear();
        foreach (var key in _physical.Keys)
            if (Math.Abs(_physical[key]) > 0.15f && (modalTransition || !MovementToken(key))) _blocked.Add(key);
        foreach (var action in ControllerActions.All)
            if (Input.IsActionPressed(action) && (modalTransition || !IsMovement(action))) _virtualBlocked.Add(action);
        Context = context;
    }

    public void Cancel()
    {
        foreach (var key in _physical.Keys) if (Math.Abs(_physical[key]) > 0.15f) _blocked.Add(key);
        foreach (var pending in _pending.Values) pending.Gesture.Cancel();
        _pending.Clear();
        _axisDown.Clear();
        _wheelToken = _wheelAction = null;
        _activeWheelOpener = null;
        foreach (var action in ControllerActions.All) Input.ActionRelease(action);
    }

    public void Disconnect(int device)
    {
        foreach (var key in _physical.Keys.Where(k => k.StartsWith($"joypad:{device}:", StringComparison.Ordinal)
            || k.StartsWith($"axis:{device}:", StringComparison.Ordinal)).ToArray())
        {
            _physical.Remove(key);
            _blocked.Remove(key);
        }
        if (device == ActiveDevice) { ActiveDevice = -1; Cancel(); }
    }

    public void Advance(double elapsedRealSeconds)
    {
        if (!double.IsFinite(elapsedRealSeconds) || elapsedRealSeconds < 0) return;
        _time += elapsedRealSeconds;
        foreach (var pair in _pending.ToArray())
            if (pair.Value.Gesture.Advance(_time) == HoldTapSignal.HoldStarted && pair.Value.Hold is { } hold)
                Emit(hold, true);
    }

    public bool Handle(InputEvent input)
    {
        if (input is InputEventMouseMotion motion)
        {
            ControllerLast = false;
            if (Context == ControlContext.Wheel) { WheelPointer?.Invoke(motion.Position); return true; }
            if (Context is ControlContext.Gameplay or ControlContext.Build)
            { PointerLook?.Invoke(motion.Relative); return true; }
            return false;
        }
        if (!TryPhysical(input, out var token, out var value)) return false;
        if (input is InputEventKey { Echo: true })
            return Context is not (ControlContext.TextEntry or ControlContext.Modal);
        var previous = _physical.GetValueOrDefault(token);
        _physical[token] = value;
        if (input is InputEventJoypadButton or InputEventJoypadMotion)
        {
            if (Math.Abs(value) > _settings.Preferences.StickDeadzone)
            { ActiveDevice = input.Device; ControllerLast = true; }
        }
        else ControllerLast = false;
        var released = Math.Abs(value) < (input is InputEventJoypadMotion ? 0.35f : 0.15f);
        if (released)
        {
            _blocked.Remove(token);
            _axisDown.Remove(token + ":1");
            _axisDown.Remove(token + ":-1");
            if (_wheelToken == token)
            {
                var action = _wheelAction!;
                _wheelToken = _wheelAction = null;
                Emit(action, false);
                return true;
            }
            if (_pending.Remove(token, out var pending))
            {
                var signal = pending.Gesture.Release(_time);
                if (signal == HoldTapSignal.Tap && pending.Tap is { } tap) Emit(tap, true);
                return true;
            }
            return false;
        }
        if (_blocked.Contains(token)) return true;
        if (Math.Abs(previous) > 0.15f && input is not InputEventJoypadMotion) return true;
        bool ActiveOpener(ControlActionDefinition definition) => Context == ControlContext.Wheel
            && _settings.Preferences.ToggleWheels && definition.Id == _activeWheelOpener;
        var candidates = ControlCatalog.All.Where(d => ControlInputPolicy.Accepts(Context, d.Contexts) || ActiveOpener(d))
            .SelectMany(d => _settings.Get(d.Id).Where(b => (ControlInputPolicy.Accepts(Context, b.Contexts ?? d.Contexts) || ActiveOpener(d)) && PhysicalMatches(b, input, value)
                && ModifierMatches(b, input.Device, input)).Select(b => (Definition: d, Binding: b))).ToArray();
        if (candidates.Any(c => c.Binding.Modifier is not null))
            candidates = candidates.Where(c => c.Binding.Modifier is not null || IsMovement(c.Definition.Id)).ToArray();
        else if ((input is InputEventJoypadButton or InputEventJoypadMotion) && PadUtilityHeld(input.Device)
            && (Context is ControlContext.Gameplay or ControlContext.Build))
            candidates = candidates.Where(c => IsMovement(c.Definition.Id) || c.Definition.Id == ControllerActions.Utility
                || c.Definition.Id == ControllerActions.Settings).ToArray();
        if (input is InputEventJoypadMotion)
        {
            if (Math.Abs(value) < 0.55f || !_axisDown.Add(token + ":" + Math.Sign(value))) return candidates.Length > 0;
        }
        var actionable = candidates.Where(c => c.Definition.Available && !Continuous(c.Definition.Id, input)).ToArray();
        var tapAction = actionable.FirstOrDefault(c => c.Binding.Gesture == "tap").Definition?.Id;
        var holdAction = actionable.FirstOrDefault(c => c.Binding.Gesture == "hold" && !IsWheel(c.Definition.Id)).Definition?.Id;
        if (tapAction is not null || holdAction is not null)
        {
            var gesture = new HoldTapGesture(_settings.Preferences.InteractionHoldSeconds);
            gesture.Press(_time);
            _pending[token] = new(gesture, tapAction, holdAction);
            return true;
        }
        foreach (var candidate in actionable)
        {
            if (candidate.Definition.Id == ControllerActions.MenuConfirm && Context == ControlContext.Modal)
                return false; // InputMap ui_accept owns focused Godot controls.
            if (IsWheel(candidate.Definition.Id))
            {
                if (_settings.Preferences.ToggleWheels) Emit(candidate.Definition.Id, true);
                else
                {
                    _wheelToken = token;
                    _wheelAction = candidate.Definition.Id;
                    Emit(candidate.Definition.Id, true);
                }
                break;
            }
            Emit(candidate.Definition.Id, true);
            break;
        }
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown })
            _physical[token] = 0;
        return candidates.Length > 0;
    }

    public float Strength(string action, bool wheelLook = false)
    {
        var definition = ControlCatalog.Get(action);
        if ((!wheelLook && !ControlInputPolicy.Accepts(Context, definition.Contexts)) || !definition.Available) return 0;
        if (_virtualBlocked.Contains(action))
        {
            if (Input.IsActionPressed(action)) return 0;
            _virtualBlocked.Remove(action);
        }
        float strength = 0;
        var physicalKnown = false;
        var bindings = _settings.Get(action).Where(binding => wheelLook
            || ControlInputPolicy.Accepts(Context, binding.Contexts ?? definition.Contexts)).ToArray();
        if (bindings.Length == 0) return 0;
        foreach (var binding in bindings)
        {
            var token = Token(binding, ActiveDevice);
            var raw = _physical.GetValueOrDefault(token);
            physicalKnown |= Math.Abs(raw) > 0.001f;
            if (_blocked.Contains(token) || (!wheelLook && !ModifierMatches(binding, ActiveDevice))) continue;
            if (!wheelLook && binding.Modifier is null && (binding.Device is "joypad" or "axis")
                && PadUtilityHeld(ActiveDevice) && !IsMovement(action) && action != ControllerActions.Utility) continue;
            if (binding.Device == "axis")
                raw = Math.Max(0, raw * binding.Sign - _settings.Preferences.StickDeadzone)
                    / (1 - _settings.Preferences.StickDeadzone);
            strength = Math.Max(strength, raw);
        }
        // Explicit Input.ActionPress callers use this same filtered boundary.
        return physicalKnown ? strength : Math.Max(strength, Input.GetActionStrength(action));
    }

    public Vector2 Movement => new(Strength(ControllerActions.MoveRight) - Strength(ControllerActions.MoveLeft),
        Strength(ControllerActions.MoveBackward) - Strength(ControllerActions.MoveForward));
    public Vector2 Look(bool wheel = false) => new(
        Strength(ControllerActions.LookRight, wheel) - Strength(ControllerActions.LookLeft, wheel),
        Strength(ControllerActions.LookDown, wheel) - Strength(ControllerActions.LookUp, wheel));

    private void Emit(string action, bool pressed) => Intent?.Invoke(new(action, pressed));
    private static bool IsMovement(string id) => id is ControllerActions.MoveForward or ControllerActions.MoveBackward
        or ControllerActions.MoveLeft or ControllerActions.MoveRight;
    private bool MovementToken(string token) => ControlCatalog.All.Where(d => IsMovement(d.Id))
        .Any(d => _settings.Get(d.Id).Any(b => Token(b, ActiveDevice) == token));
    private static bool IsWheel(string id) => id is ControllerActions.CommandWheel or ControllerActions.ItemWheel or ControllerActions.BuildWheel;
    private static bool Continuous(string id, InputEvent input) => IsMovement(id)
        || id is ControllerActions.LookLeft or ControllerActions.LookRight or ControllerActions.LookUp or ControllerActions.LookDown
        || input is InputEventJoypadMotion && (id is ControllerActions.ZoomIn or ControllerActions.ZoomOut);

    private bool PadUtilityHeld(int device) => _physical.GetValueOrDefault($"joypad:{device}:{(long)JoyButton.LeftShoulder}") > 0.5f;
    private bool ModifierMatches(ControlBinding binding, int device, InputEvent? input = null)
    {
        if (binding.Modifier is null) return true;
        if (binding.Modifier.StartsWith("pad:", StringComparison.Ordinal))
            return _physical.GetValueOrDefault($"joypad:{device}:{binding.Modifier[4..]}") > 0.5f;
        if (binding.Modifier.StartsWith("axis:", StringComparison.Ordinal))
            return _physical.GetValueOrDefault($"axis:{device}:{binding.Modifier[5..]}") > 0.5f;
        var modifiers = input as InputEventWithModifiers;
        return binding.Modifier.Split('+').All(name => name switch
        {
            "shift" => modifiers?.ShiftPressed == true || _physical.GetValueOrDefault($"key:{(long)Key.Shift}") > 0,
            "ctrl" => modifiers?.CtrlPressed == true || _physical.GetValueOrDefault($"key:{(long)Key.Ctrl}") > 0,
            "alt" => modifiers?.AltPressed == true || _physical.GetValueOrDefault($"key:{(long)Key.Alt}") > 0,
            "meta" => modifiers?.MetaPressed == true || _physical.GetValueOrDefault($"key:{(long)Key.Meta}") > 0,
            _ => false
        });
    }

    private static string Token(ControlBinding binding, int device) => binding.Device is "axis" or "joypad"
        ? $"{binding.Device}:{device}:{binding.Code}" : $"{binding.Device}:{binding.Code}";
    private static bool PhysicalMatches(ControlBinding binding, InputEvent input, float value) => input switch
    {
        InputEventKey key => binding.Device == "key" && binding.Code == (long)(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode),
        InputEventMouseButton mouse => binding.Device == "mouse" && binding.Code == (long)mouse.ButtonIndex,
        InputEventJoypadButton button => binding.Device == "joypad" && binding.Code == (long)button.ButtonIndex,
        InputEventJoypadMotion motion => binding.Device == "axis" && binding.Code == (long)motion.Axis && value * binding.Sign > 0,
        _ => false
    };
    private static bool TryPhysical(InputEvent input, out string token, out float value)
    {
        token = ""; value = 0;
        switch (input)
        {
            case InputEventKey key:
                token = $"key:{(long)(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode)}"; value = key.Pressed ? 1 : 0; return true;
            case InputEventMouseButton mouse:
                token = $"mouse:{(long)mouse.ButtonIndex}"; value = mouse.Pressed ? 1 : 0; return true;
            case InputEventJoypadButton button:
                token = $"joypad:{button.Device}:{(long)button.ButtonIndex}"; value = button.Pressed ? 1 : 0; return true;
            case InputEventJoypadMotion motion:
                token = $"axis:{motion.Device}:{(long)motion.Axis}"; value = motion.AxisValue; return true;
            default: return false;
        }
    }
}
