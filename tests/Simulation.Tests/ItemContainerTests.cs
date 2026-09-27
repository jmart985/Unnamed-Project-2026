using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class ItemContainerTests
{
    [Theory]
    [InlineData(ItemIds.Food)]
    [InlineData(ItemIds.Wood)]
    public void GroundPileEntityInventoryAndStockpileEnforceEmptyAndExactCapacity(string itemId)
    {
        foreach (var container in CreateContainers(capacity: 2))
        {
            Assert.Empty(container);
            Assert.Equal(0, container.TotalCount);
            Assert.True(container.TryGetCount(itemId, out var count));
            Assert.Equal(0, count);
            Assert.False(container.TryGetCount("ore", out _));
            Assert.Equal(ItemMutationResult.InsufficientItems, container.TryRemove(itemId, 1));

            Assert.Equal(ItemMutationResult.Success, container.TryAdd(itemId, 2));
            Assert.Equal(2, container[itemId]);
            Assert.Equal(2, container.TotalCount);
            Assert.Equal(ItemMutationResult.CapacityExceeded, container.TryAdd(itemId, 1));
            Assert.Equal(2, container[itemId]);

            Assert.Equal(ItemMutationResult.Success, container.TryRemove(itemId, 2));
            Assert.Empty(container);
            Assert.Equal(0, container.TotalCount);
        }
    }

    [Fact]
    public void CapacityIsSharedAcrossItemIdsAndRejectedMutationsDoNotChangeCounts()
    {
        foreach (var container in CreateContainers(capacity: 2))
        {
            Assert.Equal(ItemMutationResult.Success, container.TryAdd(ItemIds.Food, 1));
            Assert.Equal(ItemMutationResult.Success, container.TryAdd(ItemIds.Wood, 1));
            Assert.Equal(ItemMutationResult.CapacityExceeded, container.TryAdd(ItemIds.Wood, 1));
            Assert.Equal(1, container[ItemIds.Food]);
            Assert.Equal(1, container[ItemIds.Wood]);
            Assert.Equal(2, container.TotalCount);
        }
    }

    [Fact]
    public void UnknownItemsAndInvalidAmountsHaveExplicitOutcomesWithoutChangingCounts()
    {
        foreach (var container in CreateContainers(capacity: 2))
        {
            Assert.Equal(ItemMutationResult.UnknownItem, container.TryAdd("ore", 1));
            Assert.Equal(ItemMutationResult.UnknownItem, container.TryRemove("ore", 1));
            Assert.Equal(ItemMutationResult.UnknownItem, container.TryAdd(null, 1));
            Assert.Equal(ItemMutationResult.InvalidAmount, container.TryAdd(ItemIds.Food, 0));
            Assert.Equal(ItemMutationResult.InvalidAmount, container.TryAdd(ItemIds.Food, -1));
            Assert.Equal(ItemMutationResult.InvalidAmount, container.TryRemove(ItemIds.Food, 0));
            Assert.Equal(ItemMutationResult.InvalidAmount, container.TryRemove(ItemIds.Food, -1));
            Assert.Empty(container);
            Assert.Equal(0, container.TotalCount);
        }
    }

    [Fact]
    public void CatalogDefinitionsAreDataDrivenAndValidated()
    {
        var catalog = ItemCatalog.FromJson("{\"items\":[{\"id\":\"medicine\"}]}");
        var container = new ItemContainer(catalog, capacity: 1);

        Assert.Equal(ItemMutationResult.Success, container.TryAdd("medicine", 1));
        Assert.Equal(1, container["medicine"]);
        Assert.Contains(ItemIds.Food, ItemCatalog.Default.Definitions.Select(item => item.Id));
        Assert.Contains(ItemIds.Wood, ItemCatalog.Default.Definitions.Select(item => item.Id));
        Assert.Throws<ArgumentException>(() =>
            new ItemCatalog([new ItemDefinition("food"), new ItemDefinition("food")]));
        Assert.Throws<ArgumentException>(() => new ItemDefinition(" "));
        Assert.Throws<FormatException>(() => ItemCatalog.FromJson("{}"));
    }

    [Fact]
    public void WorldOwnsPilesAndStockpileAndResetClearsTheirContents()
    {
        var world = new World();
        var pile = world.AddGroundPile(Vector3.Zero, capacity: 2);

        Assert.Equal(ItemMutationResult.Success, pile.TryAdd(ItemIds.Wood, 1));
        Assert.Equal(ItemMutationResult.Success, world.Stockpile.TryAdd(ItemIds.Food, 1));

        world.Reset();

        Assert.Empty(world.GroundPiles);
        Assert.Empty(world.Stockpile);
        Assert.Equal(100, world.Stockpile.Capacity);
    }

    private static IEnumerable<ItemContainer> CreateContainers(int capacity)
    {
        var catalog = ItemCatalog.Default;
        var world = new World(catalog, entityInventoryCapacity: capacity, stockpileCapacity: capacity);

        yield return new GroundPile(catalog, capacity, Vector3.Zero);
        yield return world.SpawnEntity("human", Vector3.Zero).Inventory;
        yield return new Stockpile(catalog, capacity);
    }
}
