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

    [Fact]
    public void TransferMovesTheEntireQuantityWithinExactDestinationCapacity()
    {
        var source = new ResourceContainer("ground-pile", 10);
        var destination = new ResourceContainer("stockpile", 4);
        source.TryAdd(ItemIds.Wood, 5);
        destination.TryAdd(ItemIds.Food, 1);

        var result = ResourceTransferService.TryTransfer(source, destination, ItemIds.Wood, 3);

        Assert.Equal(ResourceMutationResult.Success, result);
        Assert.Equal(2, source.Counts[ItemIds.Wood]);
        Assert.Equal(1, destination.Counts[ItemIds.Food]);
        Assert.Equal(3, destination.Counts[ItemIds.Wood]);
        Assert.Equal(2, source.UsedCapacity);
        Assert.Equal(4, destination.UsedCapacity);
    }

    [Theory]
    [InlineData(ResourceMutationResult.InsufficientQuantity)]
    [InlineData(ResourceMutationResult.CapacityExceeded)]
    public void RejectedTransfersLeaveBothContainersUnchanged(ResourceMutationResult expectedResult)
    {
        var source = new ResourceContainer("ground-pile", 5);
        var destination = new ResourceContainer("stockpile", 2);
        source.TryAdd(ItemIds.Wood, expectedResult == ResourceMutationResult.InsufficientQuantity ? 1 : 3);
        if (expectedResult == ResourceMutationResult.CapacityExceeded)
        {
            destination.TryAdd(ItemIds.Food, 2);
        }

        var sourceBefore = source.Counts.ToDictionary();
        var destinationBefore = destination.Counts.ToDictionary();
        var sourceCapacityBefore = source.UsedCapacity;
        var destinationCapacityBefore = destination.UsedCapacity;

        var result = ResourceTransferService.TryTransfer(source, destination, ItemIds.Wood, 3);

        Assert.Equal(expectedResult, result);
        Assert.Equal(sourceBefore, source.Counts);
        Assert.Equal(destinationBefore, destination.Counts);
        Assert.Equal(sourceCapacityBefore, source.UsedCapacity);
        Assert.Equal(destinationCapacityBefore, destination.UsedCapacity);
    }

    [Fact]
    public void InvalidInputsAndSelfTransfersAreRejectedWithoutMutation()
    {
        var source = new ResourceContainer("ground-pile", 5);
        var destination = new ResourceContainer("stockpile", 5);
        source.TryAdd(ItemIds.Wood, 3);

        Assert.Equal(ResourceMutationResult.InvalidAmount,
            ResourceTransferService.TryTransfer(source, destination, ItemIds.Wood, 0));
        Assert.Equal(ResourceMutationResult.InvalidAmount,
            ResourceTransferService.TryTransfer(source, destination, ItemIds.Wood, -1));
        Assert.Equal(ResourceMutationResult.UnknownItem,
            ResourceTransferService.TryTransfer(source, destination, "stone", 1));
        Assert.Equal(ResourceMutationResult.InvalidDestination,
            ResourceTransferService.TryTransfer(source, source, ItemIds.Wood, 1));

        Assert.Equal(3, source.Counts[ItemIds.Wood]);
        Assert.Empty(destination.Counts);
        Assert.Equal(3, source.UsedCapacity);
        Assert.Equal(0, destination.UsedCapacity);
    }

    [Fact]
    public void RepeatedSuccessfulAndRejectedTransfersConserveTotalWood()
    {
        var source = new ResourceContainer("ground-pile", 10);
        var inventory = new ResourceContainer("entity-inventory", 4);
        var stockpile = new ResourceContainer("stockpile", 5);
        source.TryAdd(ItemIds.Wood, 7);
        var initialTotal = TotalWood(source, inventory, stockpile);

        Assert.Equal(ResourceMutationResult.Success,
            ResourceTransferService.TryTransfer(source, inventory, ItemIds.Wood, 4));
        Assert.Equal(ResourceMutationResult.Success,
            ResourceTransferService.TryTransfer(source, stockpile, ItemIds.Wood, 3));
        Assert.Equal(ResourceMutationResult.CapacityExceeded,
            ResourceTransferService.TryTransfer(inventory, stockpile, ItemIds.Wood, 3));
        Assert.Equal(ResourceMutationResult.Success,
            ResourceTransferService.TryTransfer(inventory, stockpile, ItemIds.Wood, 2));

        Assert.Equal(initialTotal, TotalWood(source, inventory, stockpile));
        Assert.Equal(0, source.Counts.GetValueOrDefault(ItemIds.Wood));
        Assert.Equal(2, inventory.Counts[ItemIds.Wood]);
        Assert.Equal(5, stockpile.Counts[ItemIds.Wood]);
    }

    private static int TotalWood(params ResourceContainer[] containers)
    {
        return containers.Sum(container => container.Counts.GetValueOrDefault(ItemIds.Wood));
    }
}
