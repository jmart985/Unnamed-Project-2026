using Godot;
using Simulation;

namespace UnnamedProject2026;

/// Installs the provisional keyboard layout used by the prototype.
/// Actions remain configurable in Godot project settings after they are created.
public static class PrototypeInputMap
{
    private static bool _installed;

    public static void EnsureDefaults()
    {
        if (_installed)
        {
            return;
        }

        _installed = true;
        AddKeyboardAction(ControllerActions.MoveForward, Key.W);
        AddKeyboardAction(ControllerActions.MoveBackward, Key.S);
        AddKeyboardAction(ControllerActions.MoveLeft, Key.A);
        AddKeyboardAction(ControllerActions.MoveRight, Key.D);
        AddKeyboardAction(ControllerActions.Jump, Key.Space);
        AddMouseAction(ControllerActions.Attack, MouseButton.Left);
        AddMouseAction(ControllerActions.AttackLeft, MouseButton.Left);
        AddMouseAction(ControllerActions.AttackRight, MouseButton.Right);
        AddKeyboardAction(ControllerActions.Collect, Key.E);
        AddKeyboardAction(ControllerActions.Interact, Key.G);
        AddKeyboardAction(ControllerActions.Inventory, Key.I);
        AddKeyboardAction(ControllerActions.Settlement, Key.Tab);
        AddKeyboardAction(ControllerActions.CloseMenu, Key.Escape);
        AddKeyboardAction(ControllerActions.DebugRoute, Key.F5);
        AddKeyboardAction(ControllerActions.DebugUnreachable, Key.F6);
        AddKeyboardAction(ControllerActions.DebugCancel, Key.F7);
        AddKeyboardAction(ControllerActions.DebugRemoveGoal, Key.F8);

        AddJoypadButtonAction(ControllerActions.Jump, JoyButton.A);
        AddJoypadButtonAction(ControllerActions.Attack, JoyButton.RightShoulder);
        AddJoypadButtonAction(ControllerActions.Collect, JoyButton.X);
        AddJoypadButtonAction(ControllerActions.Inventory, JoyButton.Y);
        AddJoypadButtonAction(ControllerActions.Settlement, JoyButton.Start);
        AddJoypadButtonAction(ControllerActions.CloseMenu, JoyButton.B);
        AddJoypadAxisAction(ControllerActions.MoveForward, JoyAxis.LeftY, -1.0f);
        AddJoypadAxisAction(ControllerActions.MoveBackward, JoyAxis.LeftY, 1.0f);
    }

    private static void AddKeyboardAction(StringName actionName, Key physicalKey, bool shift = false)
    {
        if (!InputMap.HasAction(actionName))
        {
            InputMap.AddAction(actionName);
        }

        var input = new InputEventKey { PhysicalKeycode = physicalKey, ShiftPressed = shift };
        InputMap.ActionAddEvent(actionName, input);
    }

    private static void AddJoypadButtonAction(StringName actionName, JoyButton button)
    {
        if (!InputMap.HasAction(actionName))
        {
            InputMap.AddAction(actionName);
        }

        InputMap.ActionAddEvent(actionName, new InputEventJoypadButton { ButtonIndex = button });
    }

    private static void AddMouseAction(StringName actionName, MouseButton button)
    {
        if (!InputMap.HasAction(actionName))
        {
            InputMap.AddAction(actionName);
        }

        InputMap.ActionAddEvent(actionName, new InputEventMouseButton { ButtonIndex = button });
    }

    private static void AddJoypadAxisAction(StringName actionName, JoyAxis axis, float value)
    {
        if (!InputMap.HasAction(actionName))
        {
            InputMap.AddAction(actionName);
        }

        InputMap.ActionAddEvent(actionName, new InputEventJoypadMotion { Axis = axis, AxisValue = value });
    }
}


