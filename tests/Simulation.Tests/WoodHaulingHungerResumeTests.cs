using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class WoodHaulingHungerResumeTests
{
    [Fact]
    public void HungerInterruptsLoadedTripAndDeliversCarriedWoodBeforeNewPickup()
    {
        var world = new World(
            needsTuning: new NeedsTuning(initialSatiety: 100, satietyDrainPerSecond: 1, hungryThreshold: 30, nutritionPerUnit: 50));
        var entity = world.SpawnEntity("human", Vector3.Zero, satiety: 100);
        var woodSource = AddWoodSource(world, "wood-pile", Vector3.UnitX, 2);
        var stockpile = AddWoodSource(world, "stockpile", new Vector3(2, 0, 0), 0, isStockpile: true);
        var food = new ResourceContainer("food", 2);
        Assert.Equal(ResourceMutationResult.Success, food.TryAdd(ItemIds.Food, 1));
        world.AddFoodSource("food", new Vector3(0, 0, 2), food);
        var haulNavigator = new FakeNavigationAdapter();
        var foodNavigator = new FakeNavigationAdapter();
        var hauling = new WoodHaulingService(world, _ => haulNavigator);
        var eating = new FoodConsumptionService(world, _ => foodNavigator);
        var species = GetHumanSpecies();

        Assert.Equal(WoodHaulingStatus.TravelingToSource, hauling.Advance(entity, species, 0).Status);
        var sourceRequest = haulNavigator.Requests.Keys.Single();
        Assert.True(haulNavigator.CompleteAsArrived(sourceRequest));
        Assert.True(world.TrySetEntityPosition(entity.Id, woodSource.Position));
        Assert.Equal(WoodHaulingStatus.TravelingToStockpile, hauling.Advance(entity, species, 1).Status);
        Assert.Equal(2, entity.Inventory[ItemIds.Wood]);

        entity.Needs.Advance(80);
        Assert.True(entity.Needs.IsHungry);
        Assert.Equal(FoodConsumptionStatus.Traveling, eating.Advance(entity, species, 2).Status);
        Assert.Equal(2, entity.Inventory[ItemIds.Wood]);
        var foodRequest = foodNavigator.Requests.Keys.Single();
        Assert.True(foodNavigator.CompleteAsArrived(foodRequest));
        Assert.True(world.TrySetEntityPosition(entity.Id, new Vector3(0, 0, 2)));
        Assert.Equal(FoodConsumptionStatus.Ate, eating.Advance(entity, species, 3).Status);

        var resumed = hauling.Advance(entity, species, 4);
        Assert.Equal(WoodHaulingStatus.TravelingToStockpile, resumed.Status);
        Assert.Equal(2, resumed.Amount);
        Assert.Equal("DeliveringWood", entity.Activity);
        Assert.Equal(2, entity.Inventory[ItemIds.Wood]);
        Assert.Single(haulNavigator.Requests.Values, outcome => outcome == NavigationOutcome.InProgress);

        var deliveryRequest = haulNavigator.Requests.Single(pair => pair.Value == NavigationOutcome.InProgress).Key;
        Assert.True(haulNavigator.CompleteAsArrived(deliveryRequest));
        Assert.True(world.TrySetEntityPosition(entity.Id, stockpile.Position));
        Assert.Equal(WoodHaulingStatus.Delivered, hauling.Advance(entity, species, 5).Status);
        Assert.Empty(entity.Inventory);
        Assert.Equal(2, stockpile.Contents.Counts[ItemIds.Wood]);
        Assert.Equal(0, woodSource.Contents.Counts.GetValueOrDefault(ItemIds.Wood));
    }

    private static SpeciesDefinition GetHumanSpecies() => SpeciesCatalog.LoadFromFile(
        Path.Combine(AppContext.BaseDirectory, "data", "species.json")).GetRequired("human");

    private static ResourceLocation AddWoodSource(
        World world, string id, Vector3 position, int wood, bool isStockpile = false)
    {
        var contents = new ResourceContainer(id, 2);
        if (wood > 0)
        {
            Assert.Equal(ResourceMutationResult.Success, contents.TryAdd(ItemIds.Wood, wood));
        }
        return isStockpile
            ? world.AddStockpile(id, position, contents)
            : world.AddResourcePile(id, position, contents);
    }
}
