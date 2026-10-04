using System.Text.Json;
using Godot;
using Simulation;

namespace UnnamedProject2026;

/// <summary>Player preferences are separate from world saves. Save first, then change InputMap.</summary>
public sealed class PlayerButtonBindings
{
    public sealed record ButtonBinding(string Device, long Code);
    public sealed record ActionBinding(ButtonBinding Desktop, ButtonBinding? Controller);
    public sealed record SettingsFile(int Version, Dictionary<string, ActionBinding> Actions);

    public static readonly (string Action, string Label)[] Actions =
    [
        (ControllerActions.MoveForward, "Move forward"), (ControllerActions.MoveBackward, "Move backward"),
        (ControllerActions.MoveLeft, "Move left"), (ControllerActions.MoveRight, "Move right"),
        (ControllerActions.Jump, "Jump"), (ControllerActions.AttackLeft, "Left attack"),
        (ControllerActions.AttackRight, "Right attack"), (ControllerActions.Collect, "Eat nearby food"),
        (ControllerActions.Interact, "Greet selected"), (ControllerActions.Inventory, "Inventory"),
        (ControllerActions.Settlement, "Settlement"), (ControllerActions.Settings, "Save / Settings")
    ];

    private readonly SaveSlotStore _store;
    private readonly Dictionary<string, ActionBinding> _defaults;
    private static Dictionary<string, ActionBinding>? _canonicalDefaults;
    private Dictionary<string, ActionBinding> _bindings;
    public string Path { get; set; }
    public string Status { get; private set; } = "Choose a binding, then press a key or button. Escape cancels.";

    public PlayerButtonBindings(string path, SaveSlotStore? store = null)
    {
        Path = path;
        _store = store ?? new SaveSlotStore();
        PrototypeInputMap.EnsureDefaults();
        _canonicalDefaults ??= Actions.ToDictionary(entry => entry.Action, entry =>
        {
            var events = InputMap.ActionGetEvents(entry.Action);
            var desktop = events.Select(FromEvent).First(binding => binding?.Device is "key" or "mouse")!;
            var controller = events.Select(FromEvent).FirstOrDefault(binding => binding?.Device == "joypad");
            return new ActionBinding(desktop, controller);
        });
        _defaults = new(_canonicalDefaults);
        _bindings = new(_defaults);
    }

    public ActionBinding Get(string action) => _bindings[action];

    public void Load()
    {
        if (!File.Exists(Path)) return;
        try
        {
            var file = JsonSerializer.Deserialize<SettingsFile>(_store.Load(Path));
            if (file?.Version != 1 || file.Actions is null || file.Actions.Count != Actions.Length
                || Actions.Any(entry => !file.Actions.ContainsKey(entry.Action)))
                throw new InvalidDataException("Unsupported or incomplete button mapping.");
            if (!Validate(file.Actions, out var reason)) throw new InvalidDataException(reason);
            Apply(file.Actions);
            Status = "Saved button mapping loaded.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            Status = $"Button mapping not loaded: {error.Message} Defaults remain available.";
        }
    }

    public bool TryBind(string action, bool controller, InputEvent input, out string reason)
    {
        var binding = FromEvent(input);
        if (binding is null || controller != (binding.Device == "joypad"))
        {
            reason = controller ? "Press a controller button. Escape cancels." : "Press an unmodified keyboard key or mouse button. Escape cancels.";
            return false;
        }
        var previous = _bindings[action];
        var proposed = new Dictionary<string, ActionBinding>(_bindings)
        {
            [action] = controller ? previous with { Controller = binding } : previous with { Desktop = binding }
        };
        return SaveAndApply(proposed, out reason);
    }

    public bool RestoreDefaults(out string reason) => SaveAndApply(new(_defaults), out reason);

    private bool SaveAndApply(Dictionary<string, ActionBinding> proposed, out string reason)
    {
        if (!Validate(proposed, out reason)) return false;
        try { _store.Save(Path, () => JsonSerializer.Serialize(new SettingsFile(1, proposed))); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            reason = $"Mapping not saved; current bindings retained. {error.Message}";
            return false;
        }
        Apply(proposed);
        Status = reason = "Button mapping saved.";
        return true;
    }

    private static bool Validate(Dictionary<string, ActionBinding> proposed, out string reason)
    {
        var seen = new HashSet<ButtonBinding>();
        foreach (var (action, value) in proposed)
        {
            if (value is null || value.Desktop is null || value.Desktop.Device is not ("key" or "mouse")
                || value.Controller is { Device: not "joypad" })
            {
                reason = "Invalid binding device.";
                return false;
            }
            foreach (var binding in new[] { value.Desktop, value.Controller }.OfType<ButtonBinding>())
            {
                var valid = binding.Device switch
                {
                    "key" => binding.Code > 0 && Enum.IsDefined((Key)binding.Code),
                    "mouse" => binding.Code is >= 1 and <= 3 or 8 or 9,
                    "joypad" => binding.Code is >= 0 and < (long)JoyButton.Max,
                    _ => false
                };
                var reserved = binding.Device == "key" && (Key)binding.Code is Key.Escape or Key.F5 or Key.F6 or Key.F7 or Key.F8
                    || binding.Device == "joypad" && binding.Code == (long)JoyButton.B;
                if (!valid || reserved || !seen.Add(binding))
                {
                    reason = $"{Label(binding)} is invalid, reserved, or already mapped. Choose another button for {action}.";
                    return false;
                }
            }
        }
        reason = string.Empty;
        return true;
    }

    private void Apply(Dictionary<string, ActionBinding> proposed)
    {
        foreach (var (action, binding) in proposed)
        {
            Input.ActionRelease(action);
            foreach (var input in InputMap.ActionGetEvents(action))
                if (input is InputEventKey or InputEventMouseButton or InputEventJoypadButton)
                    InputMap.ActionEraseEvent(action, input);
            InputMap.ActionAddEvent(action, ToEvent(binding.Desktop));
            if (binding.Controller is { } controller) InputMap.ActionAddEvent(action, ToEvent(controller));
        }
        // The legacy immediate-combat alias must follow the left attack binding.
        InputMap.ActionEraseEvents(ControllerActions.Attack);
        foreach (var input in InputMap.ActionGetEvents(ControllerActions.AttackLeft))
            InputMap.ActionAddEvent(ControllerActions.Attack, input);
        _bindings = new(proposed);
    }

    public static ButtonBinding? FromEvent(InputEvent input) => input switch
    {
        InputEventKey { Pressed: true, Echo: false, CtrlPressed: false, AltPressed: false, ShiftPressed: false, MetaPressed: false } key
            => new("key", (long)(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode)),
        // InputMap defaults are not pressed events.
        InputEventKey { CtrlPressed: false, AltPressed: false, ShiftPressed: false, MetaPressed: false } key when !key.Echo
            => new("key", (long)(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode)),
        InputEventMouseButton mouse => new("mouse", (long)mouse.ButtonIndex),
        InputEventJoypadButton button => new("joypad", (long)button.ButtonIndex),
        _ => null
    };

    private static InputEvent ToEvent(ButtonBinding binding) => binding.Device switch
    {
        "key" => new InputEventKey { PhysicalKeycode = (Key)binding.Code },
        "mouse" => new InputEventMouseButton { ButtonIndex = (MouseButton)binding.Code },
        "joypad" => new InputEventJoypadButton { ButtonIndex = (JoyButton)binding.Code },
        _ => throw new InvalidDataException("Unknown binding device.")
    };

    public static string Label(ButtonBinding? binding) => binding?.Device switch
    {
        "key" => OS.GetKeycodeString((Key)binding.Code),
        "mouse" => $"Mouse {(MouseButton)binding.Code}",
        "joypad" => $"Pad {(JoyButton)binding.Code}",
        _ => "Unmapped"
    };
}
