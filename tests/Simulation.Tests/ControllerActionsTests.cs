using Xunit;
using Simulation;

namespace Simulation.Tests;

public sealed class ControllerActionsTests
{
    [Fact]
    public void ActionNamesAreUniqueAndContainCoreGameplayAndMenuActions()
    {
        Assert.Equal(ControllerActions.All.Count, ControllerActions.All.Distinct().Count());
        Assert.Contains(ControllerActions.Attack, ControllerActions.All);
        Assert.Contains(ControllerActions.Collect, ControllerActions.All);
        Assert.Contains(ControllerActions.CycleTarget, ControllerActions.All);
        Assert.Contains(ControllerActions.Inventory, ControllerActions.All);
        Assert.Contains(ControllerActions.Settlement, ControllerActions.All);
        Assert.Contains(ControllerActions.CloseMenu, ControllerActions.All);
        Assert.Contains(ControllerActions.DebugRoute, ControllerActions.All);
        Assert.Contains(ControllerActions.DebugRemoveGoal, ControllerActions.All);
    }

    [Fact]
    public void ActionIdsRemainStableForSavedInputBindings()
    {
        Assert.Equal(
        [
            "move_left", "move_right", "move_forward", "move_backward", "jump",
            "combat_attack", "combat_attack_left", "combat_attack_right", "collect", "interact",
            "greet", "rest", "cycle_target", "open_inventory", "open_settlement", "open_settings",
            "close_menu", "debug_route", "debug_unreachable", "debug_cancel", "debug_remove_goal"
        ],
        ControllerActions.All);
    }

    [Fact]
    public void InventorySettlementAndSettingsAreDistinctMenusAndCollectionIsNotAMenu()
    {
        var menuState = new PlayerMenuState();
        Assert.Equal(
            new[] { PlayerMenu.Inventory, PlayerMenu.Settlement, PlayerMenu.Settings },
            Enum.GetValues<PlayerMenu>());

        menuState.Toggle(PlayerMenu.Inventory);
        Assert.Equal(PlayerMenu.Inventory, menuState.ActiveMenu);

        menuState.Toggle(PlayerMenu.Settlement);
        Assert.Equal(PlayerMenu.Settlement, menuState.ActiveMenu);

        menuState.Toggle(PlayerMenu.Settlement);
        Assert.Null(menuState.ActiveMenu);
        menuState.Toggle(PlayerMenu.Settings);
        Assert.Equal(PlayerMenu.Settings, menuState.ActiveMenu);
        menuState.Close();
        Assert.Null(menuState.ActiveMenu);
        Assert.Contains(ControllerActions.Collect, ControllerActions.All);
    }

    [Fact]
    public void InvalidMenuRequestPreservesTheCurrentScreen()
    {
        var menuState = new PlayerMenuState();
        menuState.Toggle(PlayerMenu.Settings);
        Assert.Throws<ArgumentOutOfRangeException>(() => menuState.Toggle((PlayerMenu)99));
        Assert.Equal(PlayerMenu.Settings, menuState.ActiveMenu);
    }

    [Fact]
    public void ClosingMenuClearsTheActiveMenu()
    {
        var menuState = new PlayerMenuState();
        menuState.Toggle(PlayerMenu.Inventory);

        menuState.Close();

        Assert.Null(menuState.ActiveMenu);
    }
}
