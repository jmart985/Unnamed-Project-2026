using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class FireplaceTests
{
    [Fact]
    public void FireConsumesWoodAndCooksOneMeal()
    {
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Cook"));
        var fire = session.World.AddFireplace("fire-1", session.Player.Position);
        session.Player.TryAddCarried(ItemIds.Wood, 1); session.Player.TryAddCarried(ItemIds.Plant, 1);
        var service = new FireplaceService(session.World);
        Assert.Equal(FireplaceActionCode.Success, service.AddFuel(session.Player.Id, fire.Id).Code);
        Assert.Equal(FireplaceActionCode.Success, service.CookMeal(session.Player.Id, fire.Id).Code);
        Assert.Equal(1, session.Player.Inventory.GetValueOrDefault(ItemIds.Food));
        Assert.False(fire.IsLit);
    }
}
