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
    }
}
