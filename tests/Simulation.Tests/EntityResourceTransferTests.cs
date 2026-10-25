using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class EntityResourceTransferTests
{
    [Fact]
    public void PickUpMovesTheFullAmountFromContainerToCarriedInventory()
    {
        var entity = CreateEntity();
        var groundPile = new ResourceContainer("ground-pile", 5);
        groundPile.TryAdd(ItemIds.Wood, 3);

        var result = ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 2);

        Assert.Equal(ResourceMutationResult.Success, result);
        Assert.Equal(1, groundPile.Counts[ItemIds.Wood]);
        Assert.Equal(2, entity.Inventory[ItemIds.Wood]);
    }

    [Fact]
    public void DeliverMovesTheFullAmountFromCarriedInventoryToContainer()
    {
        var entity = CreateEntity();
        var groundPile = new ResourceContainer("ground-pile", 5);
        var stockpile = new ResourceContainer("stockpile", 5);
        groundPile.TryAdd(ItemIds.Wood, 2);
        ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 2);

        var result = ResourceTransferService.TryDeliver(entity, stockpile, ItemIds.Wood, 2);

        Assert.Equal(ResourceMutationResult.Success, result);
        Assert.Empty(entity.Inventory);
        Assert.Equal(2, stockpile.Counts[ItemIds.Wood]);
    }

    [Fact]
    public void PickUpRejectsInsufficientSourceQuantityWithoutMutatingEitherSide()
    {
        var entity = CreateEntity();
        var groundPile = new ResourceContainer("ground-pile", 5);
        groundPile.TryAdd(ItemIds.Wood, 1);

        var result = ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 2);

        Assert.Equal(ResourceMutationResult.InsufficientQuantity, result);
        Assert.Equal(1, groundPile.Counts[ItemIds.Wood]);
        Assert.Empty(entity.Inventory);
    }

    [Fact]
    public void DeliverRejectsInsufficientCarriedQuantityWithoutMutatingEitherSide()
    {
        var entity = CreateEntity();
        var stockpile = new ResourceContainer("stockpile", 5);

        var result = ResourceTransferService.TryDeliver(entity, stockpile, ItemIds.Wood, 1);

        Assert.Equal(ResourceMutationResult.InsufficientQuantity, result);
        Assert.Empty(entity.Inventory);
        Assert.Empty(stockpile.Counts);
    }

    [Fact]
    public void DeliverRejectsAmountsExceedingDestinationCapacityWithoutMutatingEitherSide()
    {
        var entity = CreateEntity();
        var groundPile = new ResourceContainer("ground-pile", 5);
        var stockpile = new ResourceContainer("stockpile", 1);
        groundPile.TryAdd(ItemIds.Wood, 2);
        ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 2);

        var result = ResourceTransferService.TryDeliver(entity, stockpile, ItemIds.Wood, 2);

        Assert.Equal(ResourceMutationResult.CapacityExceeded, result);
        Assert.Equal(2, entity.Inventory[ItemIds.Wood]);
        Assert.Empty(stockpile.Counts);
    }

    [Fact]
    public void PickUpAndDeliverRejectInvalidAmountsAndUnknownItems()
    {
        var entity = CreateEntity();
        var groundPile = new ResourceContainer("ground-pile", 5);
        var stockpile = new ResourceContainer("stockpile", 5);
        groundPile.TryAdd(ItemIds.Wood, 1);

        Assert.Equal(
            ResourceMutationResult.InvalidAmount,
            ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 0));
        Assert.Equal(
            ResourceMutationResult.UnknownItem,
            ResourceTransferService.TryPickUp(groundPile, entity, "stone", 1));
        Assert.Equal(
            ResourceMutationResult.InvalidAmount,
            ResourceTransferService.TryDeliver(entity, stockpile, ItemIds.Wood, -1));
        Assert.Equal(
            ResourceMutationResult.UnknownItem,
            ResourceTransferService.TryDeliver(entity, stockpile, "stone", 1));
        Assert.Equal(1, groundPile.Counts[ItemIds.Wood]);
        Assert.Empty(entity.Inventory);
    }

    [Fact]
    public void RepeatedPickUpsAndDeliveriesConserveTotalWood()
    {
        var entity = CreateEntity();
        var groundPile = new ResourceContainer("ground-pile", 10);
        var stockpile = new ResourceContainer("stockpile", 3);
        groundPile.TryAdd(ItemIds.Wood, 5);
        var initialTotal = groundPile.Counts[ItemIds.Wood];

        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 3));
        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryDeliver(entity, stockpile, ItemIds.Wood, 3));
        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 2));
        Assert.Equal(
            ResourceMutationResult.CapacityExceeded,
            ResourceTransferService.TryDeliver(entity, stockpile, ItemIds.Wood, 2));

        var finalTotal = groundPile.Counts.GetValueOrDefault(ItemIds.Wood)
            + stockpile.Counts.GetValueOrDefault(ItemIds.Wood)
            + entity.Inventory.GetValueOrDefault(ItemIds.Wood);
        Assert.Equal(initialTotal, finalTotal);
        Assert.Equal(2, entity.Inventory[ItemIds.Wood]);
        Assert.Equal(3, stockpile.Counts[ItemIds.Wood]);
        Assert.Equal(0, groundPile.Counts.GetValueOrDefault(ItemIds.Wood));
    }

    [Fact]
    public void PickUpAcceptsOnlyTheRemainingStackSpaceAndLeavesTheRestAtTheSource()
    {
        var entity = CreateEntity();
        var filledSource = new ResourceContainer("ground-pile-full", 1000);
        filledSource.TryAdd(ItemIds.Wood, 1000);
        entity.TryAddCarried(ItemIds.Wood, 997);
        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryPickUp(filledSource, entity, ItemIds.Wood, 5, out var transferred));
        Assert.Equal(2, transferred);
        Assert.Equal(999, entity.Inventory[ItemIds.Wood]);
        Assert.Equal(998, filledSource.Counts[ItemIds.Wood]);
        Assert.Equal(999, entity.Inventory.Values.Sum());
    }

    [Fact]
    public void EntityTransferPartiallyFillsTheDestinationStack()
    {
        var source = CreateEntity();
        var destination = CreateEntity();
        source.TryAddCarried(ItemIds.Food, 5);
        destination.TryAddCarried(ItemIds.Food, 998);

        var result = ResourceTransferService.TryTransfer(source, destination, ItemIds.Food, 4, out var transferred);

        Assert.Equal(ResourceMutationResult.Success, result);
        Assert.Equal(1, transferred);
        Assert.Equal(4, source.Inventory[ItemIds.Food]);
        Assert.Equal(999, destination.Inventory[ItemIds.Food]);
    }

    private static EntityRecord CreateEntity()
    {
        var world = new World();
        return world.SpawnEntity("human", Vector3.Zero);
    }
}
