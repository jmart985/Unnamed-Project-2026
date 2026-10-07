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

    [Fact]
    public void FireplaceFuelSurvivesSaveLoad()
    {
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Fire Saver"));
        var fire = session.World.AddFireplace("fire-save", session.Player.Position);
        fire.Fuel = 3;
        var json = WorldSnapshotSerializer.Serialize(session.World, 4);
        var loaded = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Temp")).World;
        WorldSnapshotSerializer.Load(loaded, json, catalog);
        Assert.Equal(3, loaded.Fireplaces.Single(item => item.Id == fire.Id).Fuel);
    }

    [Fact]
    public void LitFireProvidesWarmthOnlyWithinItsRadius()
    {
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Warmth"));
        var fire = session.World.AddFireplace("warmth-fire", session.Player.Position);
        fire.Fuel = 1;
        var service = new FireplaceService(session.World);
        Assert.True(service.IsWarm(session.Player.Id));
        session.World.TrySetEntityPosition(session.Player.Id, fire.Position + new Vector3(0, 0, 7));
        Assert.False(service.IsWarm(session.Player.Id));
    }
}
