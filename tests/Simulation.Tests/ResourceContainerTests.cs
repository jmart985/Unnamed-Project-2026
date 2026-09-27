using Simulation;

namespace Simulation.Tests;

public sealed class ResourceContainerTests
{
    [Fact]
    public void FoodAndWoodShareTheConfiguredUnitCapacity()
    {
        var groundPile = new ResourceContainer("ground-pile", 3);
        var entityInventory = new ResourceContainer("entity-inventory", 2);
        var stockpile = new ResourceContainer("stockpile", 4);

        Assert.Equal(ResourceMutationResult.Success, groundPile.TryAdd(ItemIds.Food, 2));
        Assert.Equal(ResourceMutationResult.Success, groundPile.TryAdd(ItemIds.Wood, 1));
        Assert.Equal(ResourceMutationResult.Success, entityInventory.TryAdd(ItemIds.Wood, 2));
        Assert.Equal(ResourceMutationResult.Success, stockpile.TryAdd(ItemIds.Food, 1));
        Assert.Equal(ResourceMutationResult.Success, stockpile.TryAdd(ItemIds.Wood, 3));

        Assert.Equal(3, groundPile.UsedCapacity);
        Assert.Equal(2, entityInventory.UsedCapacity);
        Assert.Equal(4, stockpile.UsedCapacity);
        Assert.Equal(0, groundPile.RemainingCapacity);
    }

    [Fact]
    public void ExactCapacitySucceedsButAdditionalItemsAreRejectedAtomically()
    {
        var container = new ResourceContainer("stockpile", 3);

        Assert.Equal(ResourceMutationResult.Success, container.TryAdd(ItemIds.Wood, 3));
        Assert.Equal(ResourceMutationResult.CapacityExceeded, container.TryAdd(ItemIds.Food, 1));
        Assert.Equal(3, container.Counts[ItemIds.Wood]);
        Assert.False(container.Counts.ContainsKey(ItemIds.Food));
        Assert.Equal(3, container.UsedCapacity);
    }

    [Fact]
    public void InvalidAmountsAndUnknownItemsLeaveContainerUnchanged()
    {
        var container = new ResourceContainer("ground-pile", 5);

        Assert.Equal(ResourceMutationResult.InvalidAmount, container.TryAdd(ItemIds.Food, 0));
        Assert.Equal(ResourceMutationResult.InvalidAmount, container.TryRemove(ItemIds.Food, -1));
        Assert.Equal(ResourceMutationResult.UnknownItem, container.TryAdd("stone", 1));
        Assert.Equal(ResourceMutationResult.UnknownItem, container.TryRemove("stone", 1));
        Assert.Empty(container.Counts);
        Assert.Equal(0, container.UsedCapacity);
    }

    [Fact]
    public void RemovalCannotCreateNegativeCounts()
    {
        var container = new ResourceContainer("entity-inventory", 5);
        container.TryAdd(ItemIds.Food, 2);

        Assert.Equal(ResourceMutationResult.InsufficientQuantity, container.TryRemove(ItemIds.Food, 3));
        Assert.Equal(2, container.Counts[ItemIds.Food]);
        Assert.Equal(2, container.UsedCapacity);

        Assert.Equal(ResourceMutationResult.Success, container.TryRemove(ItemIds.Food, 2));
        Assert.Empty(container.Counts);
        Assert.Equal(0, container.UsedCapacity);
    }

    [Fact]
    public void CapacityAndCountQueriesRejectInvalidConstructionOrItemIds()
    {
        Assert.Throws<ArgumentException>(() => new ResourceContainer(" ", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceContainer("ground-pile", -1));

        var container = new ResourceContainer("ground-pile", 0);
        Assert.False(container.TryGetCount("stone", out var unknownCount));
        Assert.Equal(0, unknownCount);
        Assert.True(container.TryGetCount(ItemIds.Wood, out var woodCount));
        Assert.Equal(0, woodCount);
    }
}
