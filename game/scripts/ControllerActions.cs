namespace UnnamedProject2026;

/// Engine adapter action names mirror the domain action contract without requiring
/// the Godot project to compile domain source files into its generated project.
public static class ControllerActions
{
    public const string MoveLeft = "move_left";
    public const string MoveRight = "move_right";
    public const string MoveForward = "move_forward";
    public const string MoveBackward = "move_backward";
    public const string Jump = "jump";
    public const string Attack = "combat_attack";
    public const string Collect = "collect";
    public const string Inventory = "open_inventory";
    public const string Settlement = "open_settlement";
    public const string CloseMenu = "close_menu";
    public const string DebugRoute = "debug_route";
    public const string DebugUnreachable = "debug_unreachable";
    public const string DebugCancel = "debug_cancel";
    public const string DebugRemoveGoal = "debug_remove_goal";
}
