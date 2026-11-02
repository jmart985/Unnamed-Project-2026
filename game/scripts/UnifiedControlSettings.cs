using System.Text.Json;
using Godot;
using Simulation;

namespace UnnamedProject2026;

public sealed record ControlPreferences(
    bool ToggleCrouch = false, bool ToggleWheels = false,
    double InteractionHoldSeconds = 0.30, double CrouchHoldSeconds = 0.30,
    float StickDeadzone = 0.20f, float LookSensitivity = 1f, bool InvertLookY = false);

public sealed record ControlSettingsFile(int Version, Dictionary<string, ControlBinding[]> Bindings, ControlPreferences Preferences);

/// Atomic, versioned presentation preferences. Loading never mutates world state.
public sealed class UnifiedControlSettings
{
    public const int Version = 5;
    private readonly SaveSlotStore _store;
    private Dictionary<string, ControlBinding[]> _bindings = Defaults();
    public string Path { get; set; }
    public ControlPreferences Preferences { get; private set; } = new();
    public string Status { get; private set; } = "New control defaults are available.";
    public string? BackupPath { get; private set; }

    public UnifiedControlSettings(string path, SaveSlotStore? store = null)
    {
        Path = path;
        _store = store ?? new SaveSlotStore();
    }

    public IReadOnlyList<ControlBinding> Get(string action)
        => _bindings.TryGetValue(action, out var bindings) ? bindings.ToArray() : [];

    public static Dictionary<string, ControlBinding[]> Defaults()
        => ControlCatalog.All.ToDictionary(d => d.Id,
            d => new[] { d.Desktop, d.Controller }.OfType<ControlBinding>().ToArray(), StringComparer.Ordinal);

    public bool Load()
    {
        if (!File.Exists(Path)) return true;
        try
        {
            var raw = _store.Load(Path);
            using var document = JsonDocument.Parse(raw);
            if (!document.RootElement.TryGetProperty("Version", out var versionElement)
                || !versionElement.TryGetInt32(out var version)) throw new InvalidDataException("Missing controls version.");
            if (version is >= 1 and <= 4)
            {
                if (!document.RootElement.TryGetProperty("Actions", out var actions)
                    || actions.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException("Invalid legacy controls.");
                var expected = LegacyActionIds.Where(id => (version >= 4 || id != ControllerActions.BuildMode)
                    && (version >= 2 || id != ControllerActions.CycleTarget)
                    && (version >= 3 || id != ControllerActions.Greet)).ToArray();
                if (actions.EnumerateObject().Count() != expected.Length
                    || expected.Any(id => !actions.TryGetProperty(id, out var value) || value.ValueKind != JsonValueKind.Object))
                    throw new InvalidDataException("Incomplete legacy controls; original file retained.");
                var backup = Path + $".v{version}.bak";
                if (File.Exists(backup) && _store.Load(backup) != raw)
                    backup += "." + Guid.NewGuid().ToString("N");
                _store.Save(backup, () => raw);
                BackupPath = backup;
                if (!Save(Defaults(), new(), out var reason)) { Status = reason; return false; }
                Status = "New layout activated; previous controls backed up to " + backup;
                return true;
            }
            if (version != Version) throw new InvalidDataException("Unsupported controls version.");
            var settings = JsonSerializer.Deserialize<ControlSettingsFile>(raw);
            if (settings is null) throw new InvalidDataException("Empty controls file.");
            if (!Validate(settings.Bindings, settings.Preferences, out var error))
                throw new InvalidDataException(error);
            _bindings = settings.Bindings.ToDictionary(p => p.Key, p => p.Value.ToArray(), StringComparer.Ordinal);
            Preferences = settings.Preferences;
            Status = "Custom controls loaded.";
            return true;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            Status = "Controls not loaded; file preserved. " + error.Message;
            return false;
        }
    }

    public bool SetPreferences(ControlPreferences preferences, out string reason)
        => Save(_bindings, preferences, out reason);

    public bool SetBindings(string action, IEnumerable<ControlBinding> bindings, out string reason)
    {
        if (!_bindings.ContainsKey(action)) { reason = "Unknown action."; return false; }
        var proposed = _bindings.ToDictionary(p => p.Key, p => p.Value.ToArray(), StringComparer.Ordinal);
        proposed[action] = bindings.ToArray();
        return Save(proposed, Preferences, out reason);
    }

    public bool TryBind(string action, bool controller, InputEvent input, bool alternate, out string reason)
    {
        var binding = Capture(input);
        if (binding is null || controller != (binding.Device is "joypad" or "axis"))
        {
            reason = controller ? "Press a controller button or move an axis." : "Press a key or mouse button.";
            return false;
        }
        var previous = Get(action);
        var template = previous.FirstOrDefault(b => controller == (b.Device is "joypad" or "axis"));
        if (template is not null) binding = binding with { Gesture = template.Gesture };
        var retained = previous.Where(b => alternate || controller != (b.Device is "joypad" or "axis"));
        return SetBindings(action, retained.Append(binding).Distinct(), out reason);
    }

    public bool RestoreDefaults(string? category, out string reason)
    {
        var proposed = _bindings.ToDictionary(p => p.Key, p => p.Value.ToArray(), StringComparer.Ordinal);
        foreach (var definition in ControlCatalog.All.Where(d => category is null || d.Category == category))
            proposed[definition.Id] = new[] { definition.Desktop, definition.Controller }.OfType<ControlBinding>().ToArray();
        return Save(proposed, category is null ? new() : Preferences, out reason);
    }

    private bool Save(Dictionary<string, ControlBinding[]> proposed, ControlPreferences preferences, out string reason)
    {
        if (!Validate(proposed, preferences, out reason)) return false;
        try { _store.Save(Path, () => JsonSerializer.Serialize(new ControlSettingsFile(Version, proposed, preferences))); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            reason = Status = "Controls not saved; active bindings retained. " + error.Message;
            return false;
        }
        _bindings = proposed.ToDictionary(p => p.Key, p => p.Value.ToArray(), StringComparer.Ordinal);
        Preferences = preferences;
        reason = Status = "Controls saved.";
        return true;
    }

    public static bool Validate(Dictionary<string, ControlBinding[]>? bindings, ControlPreferences? preferences, out string reason)
    {
        reason = "Incomplete controls or invalid preferences.";
        if (preferences is null || !double.IsFinite(preferences.InteractionHoldSeconds)
            || preferences.InteractionHoldSeconds is < 0.10 or > 2
            || !double.IsFinite(preferences.CrouchHoldSeconds) || preferences.CrouchHoldSeconds is < 0.10 or > 2
            || !float.IsFinite(preferences.StickDeadzone) || preferences.StickDeadzone is < 0.05f or > 0.60f
            || !float.IsFinite(preferences.LookSensitivity) || preferences.LookSensitivity is < 0.1f or > 5
            || bindings is null || bindings.Count != ControlCatalog.All.Count
            || ControlCatalog.All.Any(d => !bindings.ContainsKey(d.Id))) return false;
        var seen = new List<(ControlActionDefinition Definition, ControlBinding Binding)>();
        foreach (var definition in ControlCatalog.All)
        {
            if (bindings[definition.Id] is not { Length: <= 8 } values) return false;
            foreach (var binding in values)
            {
                if (binding is null || !ValidPhysical(binding)) { reason = "Invalid physical binding for " + definition.Label; return false; }
                foreach (var other in seen)
                {
                    if (other.Definition.Id == definition.Id || other.Binding.PhysicalId != binding.PhysicalId
                        || other.Binding.Modifier != binding.Modifier
                        || (other.Definition.Contexts & definition.Contexts) == ControlContext.None
                        || (ControlCatalog.MayShare(other.Definition.Id, definition.Id)
                            && other.Binding.Gesture != binding.Gesture
                            && other.Binding.Gesture is "tap" or "hold" && binding.Gesture is "tap" or "hold")) continue;
                    reason = Label(binding) + " is already mapped to " + other.Definition.Label + " in the same context.";
                    return false;
                }
                seen.Add((definition, binding));
            }
        }
        // Every device retains a usable cancel route. Capture cancellation additionally keeps Escape.
        if (bindings[ControllerActions.CloseMenu].Length == 0) { reason = "Keep at least one Back / cancel binding."; return false; }
        reason = string.Empty;
        return true;
    }

    private static bool ValidPhysical(ControlBinding binding)
    {
        if (binding.Gesture is not ("press" or "tap" or "hold")) return false;
        if (binding.Modifier is not null)
        {
            if (binding.Device is "key" or "mouse")
            {
                var parts = binding.Modifier.Split('+');
                if (parts.Length is < 1 or > 4 || parts.Any(p => p is not ("shift" or "ctrl" or "alt" or "meta"))
                    || string.Join('+', parts.Distinct().Order(StringComparer.Ordinal)) != binding.Modifier) return false;
            }
            else if (binding.Modifier.StartsWith("pad:", StringComparison.Ordinal))
            {
                if (!long.TryParse(binding.Modifier[4..], out var button) || button < 0 || button >= (long)JoyButton.Max) return false;
            }
            else if (binding.Modifier.StartsWith("axis:", StringComparison.Ordinal))
            {
                if (!long.TryParse(binding.Modifier[5..], out var axis) || axis < 0 || axis >= (long)JoyAxis.Max) return false;
            }
            else return false;
        }
        return binding.Device switch
        {
            "key" => binding.Sign == 0 && binding.Code > 0 && Enum.IsDefined((Key)binding.Code),
            "mouse" => binding.Sign == 0 && binding.Code > 0 && Enum.IsDefined((MouseButton)binding.Code),
            "joypad" => binding.Sign == 0 && binding.Code >= 0 && binding.Code < (long)JoyButton.Max,
            "axis" => binding.Code >= 0 && binding.Code < (long)JoyAxis.Max && binding.Sign is -1 or 1,
            _ => false
        };
    }

    public static ControlBinding? Capture(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false } key)
        {
            var code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
            var modifier = KeyModifiers(key, code);
            return new("key", (long)code, Modifier: modifier);
        }
        if (input is InputEventMouseButton { Pressed: true } mouse)
            return new("mouse", (long)mouse.ButtonIndex, Modifier:
                KeyModifiers(mouse));
        if (input is InputEventJoypadButton { Pressed: true } button)
            return new("joypad", (long)button.ButtonIndex, Modifier:
                button.ButtonIndex != JoyButton.LeftShoulder && Input.IsJoyButtonPressed(button.Device, JoyButton.LeftShoulder)
                    ? ControlCatalog.PadUtility : Input.GetJoyAxis(button.Device, JoyAxis.TriggerLeft) > 0.5f
                        ? ControlCatalog.LeftTrigger : null);
        if (input is InputEventJoypadMotion motion && Math.Abs(motion.AxisValue) >= 0.65f)
            return new("axis", (long)motion.Axis, Math.Sign(motion.AxisValue), Modifier:
                Input.IsJoyButtonPressed(motion.Device, JoyButton.LeftShoulder) ? ControlCatalog.PadUtility : null);
        return null;
    }

    public void ApplyInputMap()
    {
        foreach (var definition in ControlCatalog.All)
        {
            if (!InputMap.HasAction(definition.Id)) InputMap.AddAction(definition.Id, Preferences.StickDeadzone);
            InputMap.ActionSetDeadzone(definition.Id, Preferences.StickDeadzone);
            Input.ActionRelease(definition.Id);
            InputMap.ActionEraseEvents(definition.Id);
            if (!definition.Available) continue;
            foreach (var binding in Get(definition.Id)) InputMap.ActionAddEvent(definition.Id, ToEvent(binding));
        }
    }

    public static InputEvent ToEvent(ControlBinding binding)
    {
        InputEvent result = binding.Device switch
        {
            "key" => new InputEventKey { PhysicalKeycode = (Key)binding.Code },
            "mouse" => new InputEventMouseButton { ButtonIndex = (MouseButton)binding.Code },
            "joypad" => new InputEventJoypadButton { ButtonIndex = (JoyButton)binding.Code },
            "axis" => new InputEventJoypadMotion { Axis = (JoyAxis)binding.Code, AxisValue = binding.Sign },
            _ => throw new InvalidDataException("Unknown binding device.")
        };
        if (result is InputEventWithModifiers modifiers)
        {
            var names = binding.Modifier?.Split('+') ?? [];
            modifiers.ShiftPressed = names.Contains("shift");
            modifiers.CtrlPressed = names.Contains("ctrl");
            modifiers.AltPressed = names.Contains("alt");
            modifiers.MetaPressed = names.Contains("meta");
        }
        return result;
    }

    private static string? KeyModifiers(InputEventWithModifiers input, Key own = Key.None)
    {
        var values = new List<string>();
        if (input.AltPressed && own != Key.Alt) values.Add("alt");
        if (input.CtrlPressed && own != Key.Ctrl) values.Add("ctrl");
        if (input.MetaPressed && own != Key.Meta) values.Add("meta");
        if (input.ShiftPressed && own != Key.Shift) values.Add("shift");
        return values.Count == 0 ? null : string.Join('+', values);
    }

    public static readonly string[] LegacyActionIds =
    [
        ControllerActions.MoveForward, ControllerActions.MoveBackward, ControllerActions.MoveLeft, ControllerActions.MoveRight,
        ControllerActions.Jump, ControllerActions.AttackLeft, ControllerActions.AttackRight, ControllerActions.Collect,
        ControllerActions.Interact, ControllerActions.Greet, ControllerActions.CycleTarget, ControllerActions.Inventory,
        ControllerActions.Settlement, ControllerActions.BuildMode, ControllerActions.Settings
    ];

    public static string Label(ControlBinding? binding)
    {
        if (binding is null) return "Unmapped";
        var value = binding.Device switch
        {
            "key" => OS.GetKeycodeString((Key)binding.Code),
            "mouse" => binding.Code switch { 1 => "LMB", 2 => "RMB", 3 => "MMB", _ => ((MouseButton)binding.Code).ToString() },
            "joypad" => ((JoyButton)binding.Code) switch
            {
                JoyButton.A => "A / Cross", JoyButton.B => "B / Circle",
                JoyButton.X => "X / Square", JoyButton.Y => "Y / Triangle",
                JoyButton.LeftShoulder => "LB / L1", JoyButton.RightShoulder => "RB / R1",
                JoyButton.Back => "View / Touchpad", JoyButton.Start => "Menu / Options",
                JoyButton.LeftStick => "L3", JoyButton.RightStick => "R3", _ => ((JoyButton)binding.Code).ToString()
            },
            "axis" => ((JoyAxis)binding.Code) switch
            {
                JoyAxis.TriggerLeft => "LT / L2", JoyAxis.TriggerRight => "RT / R2",
                _ => ((JoyAxis)binding.Code) + (binding.Sign < 0 ? " −" : " +")
            },
            _ => "Unmapped"
        };
        var prefix = binding.Modifier == ControlCatalog.PadUtility ? "LB + "
            : binding.Modifier == ControlCatalog.LeftTrigger ? "LT + "
            : binding.Modifier is null ? "" : binding.Modifier + " + ";
        return prefix + value + (binding.Gesture == "press" ? "" : " (" + binding.Gesture + ")");
    }
}
