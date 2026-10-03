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
        Assert.Contains(ControllerActions.Inventory, ControllerActions.All);
        Assert.Contains(ControllerActions.Settlement, ControllerActions.All);
        Assert.Contains(ControllerActions.CloseMenu, ControllerActions.All);
        Assert.Contains(ControllerActions.DebugRoute, ControllerActions.All);
        Assert.Contains(ControllerActions.DebugRemoveGoal, ControllerActions.All);
    }

    [Fact]
    public void InventoryAndSettlementAreDistinctMenusAndCollectionIsNotAMenu()
    {
        var menuState = new PlayerMenuState();
        Assert.Equal(
            new[] { PlayerMenu.Inventory, PlayerMenu.Settlement },
            Enum.GetValues<PlayerMenu>());

        menuState.Toggle(PlayerMenu.Inventory);
        Assert.Equal(PlayerMenu.Inventory, menuState.ActiveMenu);

        menuState.Toggle(PlayerMenu.Settlement);
        Assert.Equal(PlayerMenu.Settlement, menuState.ActiveMenu);

        menuState.Toggle(PlayerMenu.Settlement);
        Assert.Null(menuState.ActiveMenu);
        Assert.Contains(ControllerActions.Collect, ControllerActions.All);
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
