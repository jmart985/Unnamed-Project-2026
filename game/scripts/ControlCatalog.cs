using Godot;
using Simulation;

namespace UnnamedProject2026;

/// Physical bindings belong to presentation preferences, never world saves.
public sealed record ControlBinding(string Device, long Code, int Sign = 0, string? Modifier = null, string Gesture = "press")
{
    public string PhysicalId => $"{Device}:{Code}:{Sign}";
}

public sealed record ControlActionDefinition(string Id, string Label, string Category, ControlContext Contexts,
    ControlBinding? Desktop, ControlBinding? Controller, bool Available);

/// One source for defaults, settings, help and contextual prompts. Reserved actions do not execute.
public static class ControlCatalog
{
    public static readonly string PadUtility = $"pad:{(long)JoyButton.LeftShoulder}";
    public static readonly string LeftTrigger = $"axis:{(long)JoyAxis.TriggerLeft}";
    public static IReadOnlyList<ControlActionDefinition> All { get; } =
    [
        new(ControllerActions.MoveForward, "Move forward", "Movement", ControlContext.Play, K(Key.W), A(JoyAxis.LeftY, -1), true),
        new(ControllerActions.MoveBackward, "Move backward", "Movement", ControlContext.Play, K(Key.S), A(JoyAxis.LeftY, 1), true),
        new(ControllerActions.MoveLeft, "Move left", "Movement", ControlContext.Play, K(Key.A), A(JoyAxis.LeftX, -1), true),
        new(ControllerActions.MoveRight, "Move right", "Movement", ControlContext.Play, K(Key.D), A(JoyAxis.LeftX, 1), true),
        new(ControllerActions.Jump, "Jump / vault", "Movement", ControlContext.Play, K(Key.Space), P(JoyButton.A), true),
        new(ControllerActions.Sprint, "Sprint", "Movement", ControlContext.Play, K(Key.Shift), P(JoyButton.LeftStick), false),
        new(ControllerActions.Crouch, "Crouch (hold / optional toggle)", "Movement", ControlContext.Gameplay, K(Key.Ctrl, gesture: "hold"), P(JoyButton.B, gesture: "hold"), false),
        new(ControllerActions.Dodge, "Dodge (tap)", "Combat", ControlContext.Gameplay, K(Key.Alt), P(JoyButton.B, gesture: "tap"), false),
        new(ControllerActions.PrimaryAction, "Primary equipment action", "Combat", ControlContext.Gameplay, M(MouseButton.Left), A(JoyAxis.TriggerRight, 1), true),
        new(ControllerActions.SecondaryAction, "Secondary / offhand action", "Combat", ControlContext.Gameplay, M(MouseButton.Right), A(JoyAxis.TriggerLeft, 1), true),
        new(ControllerActions.AlternateAction, "Alternate equipment action", "Combat", ControlContext.Gameplay, K(Key.R), A(JoyAxis.TriggerRight, 1, PadUtility), false),
        new(ControllerActions.ReadyWeapon, "Ready / stow weapon", "Combat", ControlContext.Gameplay, K(Key.X), P(JoyButton.Y), false),
        new(ControllerActions.SwitchWeapon, "Switch weapon set", "Combat", ControlContext.Gameplay, K(Key.Z), P(JoyButton.DpadDown), false),
        new(ControllerActions.LockTarget, "Lock / unlock target", "Interaction", ControlContext.Gameplay, M(MouseButton.Middle), P(JoyButton.RightStick), true),
        new(ControllerActions.CycleTarget, "Next target", "Interaction", ControlContext.Gameplay, K(Key.T), null, true),
        new(ControllerActions.PreviousTarget, "Previous target", "Interaction", ControlContext.Gameplay, K(Key.T, "shift"), null, true),
        new(ControllerActions.DefaultInteraction, "Interact (tap)", "Interaction", ControlContext.Gameplay, K(Key.E, gesture: "tap"), P(JoyButton.X, gesture: "tap"), true),
        new(ControllerActions.ContextInteraction, "Target interaction menu (hold)", "Interaction", ControlContext.Gameplay, K(Key.E, gesture: "hold"), P(JoyButton.X, gesture: "hold"), true),
        new(ControllerActions.Inspect, "Inspect focused target", "Interaction", ControlContext.Gameplay, K(Key.H), P(JoyButton.X, PadUtility), true),
        new(ControllerActions.ItemWheel, "Quickslot wheel (hold)", "Quickslots", ControlContext.Gameplay, K(Key.Tab, gesture: "hold"), P(JoyButton.RightShoulder, gesture: "hold"), false),
        new(ControllerActions.CommandWheel, "Squad command wheel (hold)", "Commands", ControlContext.Gameplay, K(Key.Q, gesture: "hold"), P(JoyButton.RightShoulder, PadUtility, "hold"), true),
        new(ControllerActions.SocialWheel, "Self / social actions", "Interaction", ControlContext.Gameplay, K(Key.G), P(JoyButton.DpadDown, PadUtility), true),
        new(ControllerActions.UseQuickslot, "Use selected quickslot", "Quickslots", ControlContext.Gameplay, K(Key.F), P(JoyButton.DpadUp), false),
        new(ControllerActions.PreviousSlot, "Previous quickslot", "Quickslots", ControlContext.Gameplay, null, P(JoyButton.DpadLeft), false),
        new(ControllerActions.NextSlot, "Next quickslot", "Quickslots", ControlContext.Gameplay, null, P(JoyButton.DpadRight), false),
        new(ControllerActions.Inventory, "Inventory / equipment", "Menus", ControlContext.Play, K(Key.I), P(JoyButton.Back), true),
        new(ControllerActions.Settlement, "Settlement", "Menus", ControlContext.Play, K(Key.N), P(JoyButton.DpadRight, PadUtility), true),
        new(ControllerActions.Settings, "Pause / settings", "Menus", ControlContext.Gameplay, K(Key.Escape), P(JoyButton.Start), true),
        new(ControllerActions.Help, "Controls reference", "Menus", ControlContext.Play, K(Key.F1), null, true),
        new(ControllerActions.Map, "Map", "Menus", ControlContext.Play, K(Key.M), P(JoyButton.DpadUp, PadUtility), false),
        new(ControllerActions.Skills, "Character / skills", "Menus", ControlContext.Play, K(Key.K), P(JoyButton.DpadLeft, PadUtility), false),
        new(ControllerActions.Journal, "Journal", "Menus", ControlContext.Play, K(Key.J), null, false),
        new(ControllerActions.Crafting, "Personal crafting", "Menus", ControlContext.Gameplay, K(Key.C), null, false),
        new(ControllerActions.ToggleView, "First / third person", "Camera", ControlContext.Play, K(Key.V), P(JoyButton.A, PadUtility), true),
        new(ControllerActions.LookLeft, "Look left", "Camera", ControlContext.Play, null, A(JoyAxis.RightX, -1), true),
        new(ControllerActions.LookRight, "Look right", "Camera", ControlContext.Play, null, A(JoyAxis.RightX, 1), true),
        new(ControllerActions.LookUp, "Look up", "Camera", ControlContext.Play, null, A(JoyAxis.RightY, -1), true),
        new(ControllerActions.LookDown, "Look down", "Camera", ControlContext.Play, null, A(JoyAxis.RightY, 1), true),
        new(ControllerActions.ZoomIn, "Zoom in", "Camera", ControlContext.Play, M(MouseButton.WheelUp), A(JoyAxis.RightY, -1, PadUtility), true),
        new(ControllerActions.ZoomOut, "Zoom out", "Camera", ControlContext.Play, M(MouseButton.WheelDown), A(JoyAxis.RightY, 1, PadUtility), true),
        new(ControllerActions.Pause, "Pause / resume simulation", "Time", ControlContext.Play, K(Key.P), P(JoyButton.Back, PadUtility), true),
        new(ControllerActions.Slower, "Slower simulation", "Time", ControlContext.Play, K(Key.Minus), null, true),
        new(ControllerActions.Faster, "Faster simulation", "Time", ControlContext.Play, K(Key.Equal), null, true),
        new(ControllerActions.QuickSave, "Quicksave", "Time", ControlContext.Play, K(Key.F5), null, true),
        new(ControllerActions.QuickLoad, "Quickload (confirm)", "Time", ControlContext.Play, K(Key.F9), null, true),
        new(ControllerActions.BuildMode, "Build mode", "Build", ControlContext.Play, K(Key.B), P(JoyButton.Y, PadUtility), true),
        new(ControllerActions.BuildPlace, "Place preview", "Build", ControlContext.Build, M(MouseButton.Left), A(JoyAxis.TriggerRight, 1), true),
        new(ControllerActions.BuildCancel, "Cancel preview / leave build", "Build", ControlContext.Build, M(MouseButton.Right), P(JoyButton.B), true),
        new(ControllerActions.BuildCatalog, "Build catalog", "Build", ControlContext.Build, K(Key.C), P(JoyButton.X), true),
        new(ControllerActions.BuildRotateLeft, "Rotate preview left", "Build", ControlContext.Build, K(Key.Q), P(JoyButton.DpadLeft, LeftTrigger), true),
        new(ControllerActions.BuildRotateRight, "Rotate preview right", "Build", ControlContext.Build, K(Key.R), P(JoyButton.DpadRight, LeftTrigger), true),
        new(ControllerActions.BuildSnap, "Toggle snapping", "Build", ControlContext.Build, K(Key.G), P(JoyButton.Y), false),
        new(ControllerActions.BuildPrevious, "Previous build slot", "Build", ControlContext.Build, null, P(JoyButton.DpadLeft), true),
        new(ControllerActions.BuildNext, "Next build slot", "Build", ControlContext.Build, null, P(JoyButton.DpadRight), true),
        new(ControllerActions.BuildWheel, "Build wheel (hold)", "Build", ControlContext.Build, null, P(JoyButton.RightShoulder, gesture: "hold"), true),
        new(ControllerActions.CloseMenu, "Back / cancel", "Menus", ControlContext.Modal | ControlContext.Wheel | ControlContext.TextEntry | ControlContext.Rebinding, K(Key.Escape), P(JoyButton.B), true),
        new(ControllerActions.Utility, "Utility modifier", "Menus", ControlContext.Play, null, P(JoyButton.LeftShoulder), true),
        new(ControllerActions.Greet, "Greet (interaction menu)", "Interaction", ControlContext.Gameplay, null, null, true),
        new(ControllerActions.Rest, "Rest / wake (self menu)", "Interaction", ControlContext.Gameplay, null, null, true),
        new(ControllerActions.Quickslot1, "Quickslot 1", "Quickslots", ControlContext.Gameplay, K(Key.Key1), null, false),
        new(ControllerActions.BuildSlot1, "Build slot 1", "Build", ControlContext.Build, K(Key.Key1), null, true),
        new(ControllerActions.Quickslot2, "Quickslot 2", "Quickslots", ControlContext.Gameplay, K(Key.Key2), null, false),
        new(ControllerActions.BuildSlot2, "Build slot 2", "Build", ControlContext.Build, K(Key.Key2), null, true),
        new(ControllerActions.Quickslot3, "Quickslot 3", "Quickslots", ControlContext.Gameplay, K(Key.Key3), null, false),
        new(ControllerActions.BuildSlot3, "Build slot 3", "Build", ControlContext.Build, K(Key.Key3), null, true),
        new(ControllerActions.Quickslot4, "Quickslot 4", "Quickslots", ControlContext.Gameplay, K(Key.Key4), null, false),
        new(ControllerActions.BuildSlot4, "Build slot 4", "Build", ControlContext.Build, K(Key.Key4), null, true),
        new(ControllerActions.Quickslot5, "Quickslot 5", "Quickslots", ControlContext.Gameplay, K(Key.Key5), null, false),
        new(ControllerActions.BuildSlot5, "Build slot 5", "Build", ControlContext.Build, K(Key.Key5), null, true),
        new(ControllerActions.Quickslot6, "Quickslot 6", "Quickslots", ControlContext.Gameplay, K(Key.Key6), null, false),
        new(ControllerActions.BuildSlot6, "Build slot 6", "Build", ControlContext.Build, K(Key.Key6), null, true),
        new(ControllerActions.Quickslot7, "Quickslot 7", "Quickslots", ControlContext.Gameplay, K(Key.Key7), null, false),
        new(ControllerActions.BuildSlot7, "Build slot 7", "Build", ControlContext.Build, K(Key.Key7), null, true),
        new(ControllerActions.Quickslot8, "Quickslot 8", "Quickslots", ControlContext.Gameplay, K(Key.Key8), null, false),
        new(ControllerActions.BuildSlot8, "Build slot 8", "Build", ControlContext.Build, K(Key.Key8), null, true),
        new(ControllerActions.Quickslot9, "Quickslot 9", "Quickslots", ControlContext.Gameplay, K(Key.Key9), null, false),
        new(ControllerActions.BuildSlot9, "Build slot 9", "Build", ControlContext.Build, K(Key.Key9), null, true),
        new(ControllerActions.Quickslot10, "Quickslot 10", "Quickslots", ControlContext.Gameplay, K(Key.Key0), null, false),
        new(ControllerActions.BuildSlot10, "Build slot 10", "Build", ControlContext.Build, K(Key.Key0), null, true),
    ];

    public static ControlActionDefinition Get(string id) => All.First(definition => definition.Id == id);
    public static ControlBinding K(Key key, string? modifier = null, string gesture = "press")
        => new("key", (long)key, Modifier: modifier, Gesture: gesture);
    public static ControlBinding M(MouseButton button) => new("mouse", (long)button);
    public static ControlBinding P(JoyButton button, string? modifier = null, string gesture = "press")
        => new("joypad", (long)button, Modifier: modifier, Gesture: gesture);
    public static ControlBinding A(JoyAxis axis, int sign, string? modifier = null)
        => new("axis", (long)axis, sign, modifier);

    public static bool MayShare(string firstId, string secondId)
        => firstId != secondId && (new[] { firstId, secondId }.All(id =>
            id is ControllerActions.Crouch or ControllerActions.Dodge)
            || new[] { firstId, secondId }.All(id =>
                id is ControllerActions.DefaultInteraction or ControllerActions.ContextInteraction));
}

