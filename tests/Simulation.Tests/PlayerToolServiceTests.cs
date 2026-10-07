using Simulation;

namespace Simulation.Tests;

public sealed class PlayerToolServiceTests
{
    [Fact]
    public void CraftsAxeFromTwoWoodAndOneRock()
    {
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Tool Tester"));
        session.Player.TryAddCarried(ItemIds.Wood, 2);
        session.Player.TryAddCarried(ItemIds.Rock, 1);

        var result = new PlayerToolService(session.World).CraftAxe();

        Assert.Equal(ToolCraftResultCode.Crafted, result.Code);
        Assert.Equal(1, session.Player.Inventory[ItemIds.Axe]);
        Assert.Equal(0, session.Player.Inventory.GetValueOrDefault(ItemIds.Wood));
        Assert.Equal(0, session.Player.Inventory.GetValueOrDefault(ItemIds.Rock));
    }
}
