namespace UnnamedProject2026;

/// Compatibility facade for Godot scripts. Simulation owns the stable action-name values.
public static class ControllerActions
{
    public const string MoveLeft = Simulation.ControllerActions.MoveLeft;
    public const string MoveRight = Simulation.ControllerActions.MoveRight;
    public const string MoveForward = Simulation.ControllerActions.MoveForward;
    public const string MoveBackward = Simulation.ControllerActions.MoveBackward;
    public const string Jump = Simulation.ControllerActions.Jump;
    public const string Attack = Simulation.ControllerActions.Attack;
    public const string AttackLeft = Simulation.ControllerActions.AttackLeft;
    public const string AttackRight = Simulation.ControllerActions.AttackRight;
    public const string Collect = Simulation.ControllerActions.Collect;
    public const string Interact = Simulation.ControllerActions.Interact;
    public const string Greet = Simulation.ControllerActions.Greet;
    public const string Rest = Simulation.ControllerActions.Rest;
    public const string CycleTarget = Simulation.ControllerActions.CycleTarget;
    public const string Inventory = Simulation.ControllerActions.Inventory;
    public const string Settlement = Simulation.ControllerActions.Settlement;
    public const string BuildMode = Simulation.ControllerActions.BuildMode;
    public const string Settings = Simulation.ControllerActions.Settings;
    public static IReadOnlyList<string> All => Simulation.ControllerActions.All;
    public const string CloseMenu = Simulation.ControllerActions.CloseMenu;
    public const string DebugRoute = Simulation.ControllerActions.DebugRoute;
    public const string DebugUnreachable = Simulation.ControllerActions.DebugUnreachable;
    public const string DebugCancel = Simulation.ControllerActions.DebugCancel;
    public const string DebugRemoveGoal = Simulation.ControllerActions.DebugRemoveGoal;
}

