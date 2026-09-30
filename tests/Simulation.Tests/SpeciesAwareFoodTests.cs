using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class SpeciesAwareFoodTests
{
    [Fact]
    public void CatalogBackedSelectionResolvesHumanAndCreatureDiets()
    {
        var catalog = LoadCatalog();
        var world = new World();
        var human = world.SpawnEntity("human", Vector3.Zero, satiety: 10);
        var creature = world.SpawnEntity("creature", Vector3.UnitX, satiety: 10);
        var contents = new ResourceContainer("food", 10);
        contents.TryAdd(ItemIds.Food, 2);
        var food = new FoodSource("food", new(2, 0, 0), contents);
        var service = new FoodSelectionService(world, catalog);

        Assert.Equal(FoodSelectionStatus.Selected, service.TrySelect(human, [food], 0).Status);
        Assert.Equal(FoodSelectionStatus.Selected, service.TrySelect(creature, [food], 0).Status);
    }

    [Fact]
    public void MissingDietItemProducesStableWaitingReason()
    {
        var catalog = LoadCatalog();
        var world = new World();
        var creature = world.SpawnEntity("creature", Vector3.Zero, satiety: 10);
        var contents = new ResourceContainer("wood", 10);
        contents.TryAdd(ItemIds.Wood, 2);
        var wood = new FoodSource("wood", Vector3.UnitX, contents);
        var service = new FoodSelectionService(world, catalog);

        var first = service.TrySelect(creature, [wood], 0);
        var second = service.TrySelect(creature, [wood], 1);
        Assert.Equal(FoodSelectionStatus.Waiting, first.Status);
        Assert.Equal("No compatible food available.", first.WaitingReason);
        Assert.Equal(first.WaitingReason, second.WaitingReason);
    }

    private static SpeciesCatalog LoadCatalog() => SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
}
