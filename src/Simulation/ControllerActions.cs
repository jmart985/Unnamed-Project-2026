namespace Simulation;

/// Stable action names shared by gameplay code and the Godot input adapter.
/// Keeping these names in the domain prevents each menu from inventing its own bindings.
public static class ControllerActions
{
    public const string MoveLeft = "move_left";
    public const string MoveRight = "move_right";
    public const string MoveForward = "move_forward";
    public const string MoveBackward = "move_backward";
    public const string Jump = "jump";
    public const string Attack = "combat_attack";
    public const string AttackLeft = "combat_attack_left";
    public const string AttackRight = "combat_attack_right";
    public const string Collect = "collect";
    public const string Interact = "interact";
    public const string Greet = "greet";
    public const string Rest = "rest";
    public const string CycleTarget = "cycle_target";
    public const string Inventory = "open_inventory";
    public const string Settlement = "open_settlement";
    public const string BuildMode = "open_build_mode";
    public const string Settings = "open_settings";
    public const string CloseMenu = "close_menu";
    public const string DebugRoute = "debug_route";
    public const string DebugUnreachable = "debug_unreachable";
    public const string DebugCancel = "debug_cancel";
    public const string DebugRemoveGoal = "debug_remove_goal";

    public const string PrimaryAction = "equipment_primary";
    public const string SecondaryAction = "equipment_secondary";
    public const string AlternateAction = "equipment_alternate";
    public const string DefaultInteraction = "interaction_default";
    public const string ContextInteraction = "interaction_context";
    public const string Inspect = "inspect_target";
    public const string Sprint = "sprint";
    public const string Crouch = "crouch";
    public const string Dodge = "dodge";
    public const string ReadyWeapon = "weapon_ready";
    public const string SwitchWeapon = "weapon_switch";
    public const string LockTarget = "target_lock";
    public const string PreviousTarget = "target_previous";
    public const string ItemWheel = "open_item_wheel";
    public const string CommandWheel = "open_command_wheel";
    public const string SocialWheel = "open_social_wheel";
    public const string UseQuickslot = "quickslot_use";
    public const string PreviousSlot = "quickslot_previous";
    public const string NextSlot = "quickslot_next";
    public const string Map = "open_map";
    public const string Skills = "open_skills";
    public const string Journal = "open_journal";
    public const string Crafting = "open_crafting";
    public const string ToggleView = "camera_toggle_view";
    public const string LookLeft = "look_left";
    public const string LookRight = "look_right";
    public const string LookUp = "look_up";
    public const string LookDown = "look_down";
    public const string ZoomIn = "camera_zoom_in";
    public const string ZoomOut = "camera_zoom_out";
    public const string Pause = "simulation_pause";
    public const string Slower = "simulation_slower";
    public const string Faster = "simulation_faster";
    public const string QuickSave = "quick_save";
    public const string QuickLoad = "quick_load";
    public const string Help = "open_controls";
    public const string BuildPlace = "build_place";
    public const string BuildCancel = "build_cancel";
    public const string BuildCatalog = "build_catalog";
    public const string BuildRotateLeft = "build_rotate_left";
    public const string BuildRotateRight = "build_rotate_right";
    public const string BuildSnap = "build_snap";
    public const string BuildPrevious = "build_previous";
    public const string BuildNext = "build_next";
    public const string BuildWheel = "build_wheel";
    public const string Utility = "utility_modifier";
    public const string Quickslot1 = "quickslot_1";
    public const string BuildSlot1 = "build_slot_1";
    public const string Quickslot2 = "quickslot_2";
    public const string BuildSlot2 = "build_slot_2";
    public const string Quickslot3 = "quickslot_3";
    public const string BuildSlot3 = "build_slot_3";
    public const string Quickslot4 = "quickslot_4";
    public const string BuildSlot4 = "build_slot_4";
    public const string Quickslot5 = "quickslot_5";
    public const string BuildSlot5 = "build_slot_5";
    public const string Quickslot6 = "quickslot_6";
    public const string BuildSlot6 = "build_slot_6";
    public const string Quickslot7 = "quickslot_7";
    public const string BuildSlot7 = "build_slot_7";
    public const string Quickslot8 = "quickslot_8";
    public const string BuildSlot8 = "build_slot_8";
    public const string Quickslot9 = "quickslot_9";
    public const string BuildSlot9 = "build_slot_9";
    public const string Quickslot10 = "quickslot_10";
    public const string BuildSlot10 = "build_slot_10";

    public static IReadOnlyList<string> All { get; } =
    [
        MoveLeft, MoveRight, MoveForward, MoveBackward, Jump,
        Attack, AttackLeft, AttackRight, Collect, Interact, Greet, Rest, CycleTarget, Inventory, Settlement,
        Settings, CloseMenu,
        DebugRoute, DebugUnreachable, DebugCancel, DebugRemoveGoal, BuildMode,
        PrimaryAction,
        SecondaryAction,
        AlternateAction,
        DefaultInteraction,
        ContextInteraction,
        Inspect,
        Sprint,
        Crouch,
        Dodge,
        ReadyWeapon,
        SwitchWeapon,
        LockTarget,
        PreviousTarget,
        ItemWheel,
        CommandWheel,
        SocialWheel,
        UseQuickslot,
        PreviousSlot,
        NextSlot,
        Map,
        Skills,
        Journal,
        Crafting,
        ToggleView,
        LookLeft,
        LookRight,
        LookUp,
        LookDown,
        ZoomIn,
        ZoomOut,
        Pause,
        Slower,
        Faster,
        QuickSave,
        QuickLoad,
        Help,
        BuildPlace,
        BuildCancel,
        BuildCatalog,
        BuildRotateLeft,
        BuildRotateRight,
        BuildSnap,
        BuildPrevious,
        BuildNext,
        BuildWheel,
        Utility,
        Quickslot1,
        BuildSlot1,
        Quickslot2,
        BuildSlot2,
        Quickslot3,
        BuildSlot3,
        Quickslot4,
        BuildSlot4,
        Quickslot5,
        BuildSlot5,
        Quickslot6,
        BuildSlot6,
        Quickslot7,
        BuildSlot7,
        Quickslot8,
        BuildSlot8,
        Quickslot9,
        BuildSlot9,
        Quickslot10,
        BuildSlot10,
    ];
}


