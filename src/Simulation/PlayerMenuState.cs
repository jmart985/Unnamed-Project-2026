namespace Simulation;

public enum PlayerMenu
{
    Inventory,
    Settlement,
    Settings
}

public sealed class PlayerMenuState
{
    public PlayerMenu? ActiveMenu { get; private set; }

    public void Toggle(PlayerMenu menu)
    {
        if (!Enum.IsDefined(menu))
        {
            throw new ArgumentOutOfRangeException(nameof(menu));
        }

        ActiveMenu = ActiveMenu == menu ? null : menu;
    }

    public void Close() => ActiveMenu = null;
}
